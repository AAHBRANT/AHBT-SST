import { useEffect, useState } from 'react';
import { Button, FeedbackInline, Spinner, useTipografia } from '@ui';
import { Checkmark16Regular, CheckmarkCircle24Filled, PlugConnected20Regular } from '@fluentui/react-icons';
import { StreamCamera } from '../../lib/api';
import { mergeClasses, useEstilosCameraIp } from './estilosCameraIp';
import { useTesteConexaoCamera } from './useTesteConexaoCamera';

export interface TesteConexaoCameraProps {
  // Falso enquanto falta escolher equipamento ou lente: o botão fica travado e a dica explica o que falta.
  podeTestar: boolean;
  dicaFalta?: string;
  lente: number | null;
  stream: number;
  // Muda quando o equipamento, a lente ou a qualidade mudam: o resultado anterior deixa de valer.
  chaveResultado: string;
  solicitar: () => Promise<{ id: string }>;
}

// "Testar conexão" da câmera IP. A miniatura vem do servidor uma única vez e fica só na memória deste componente.
export function TesteConexaoCamera({ podeTestar, dicaFalta, lente, stream, chaveResultado, solicitar }: TesteConexaoCameraProps) {
  const e = useEstilosCameraIp();
  const tipo = useTipografia();
  const { fase, miniaturaBase64, falha, iniciar, reiniciar } = useTesteConexaoCamera();
  const [dimensoes, setDimensoes] = useState<{ largura: number; altura: number } | null>(null);

  useEffect(() => {
    reiniciar();
  }, [chaveResultado, reiniciar]);

  const testando = fase === 'enviando' || fase === 'aguardando';
  const qualidade = stream === StreamCamera.Principal ? 'principal' : 'secundária';

  return (
    <div>
      <span className={mergeClasses(tipo.corpo, e.rotuloCampo)}>Conexão com a câmera</span>
      <div className={e.teste}>
        <Button
          className={e.botao}
          icon={testando ? <Spinner size="tiny" /> : <PlugConnected20Regular />}
          disabled={!podeTestar || testando}
          onClick={() => {
            setDimensoes(null);
            void iniciar(solicitar);
          }}
        >
          {testando ? 'Testando…' : 'Testar conexão'}
        </Button>
        <div className={e.testeResultado} aria-live="polite">
          {fase === 'parado' && (
            <div className={mergeClasses(tipo.legenda, e.dica)} style={{ marginTop: 12 }}>
              {podeTestar
                ? 'Confira se a câmera responde antes de salvar. O teste usa o agente da obra e leva até 1 minuto.'
                : (dicaFalta ?? 'Escolha o equipamento e a lente para testar.')}
            </div>
          )}

          {testando && (
            <>
              <div className={e.passos}>
                <Passo estado={fase === 'enviando' ? 'andamento' : 'feito'} numero={1}>
                  Pedido enviado ao agente
                </Passo>
                <Passo estado={fase === 'enviando' ? 'pendente' : 'andamento'} numero={2}>
                  Agente lendo a câmera…
                </Passo>
                <Passo estado="pendente" numero={3}>
                  Resultado
                </Passo>
              </div>
              <div className={mergeClasses(tipo.legenda, e.dica)} style={{ marginTop: 8 }}>
                Pode continuar preenchendo; o resultado aparece aqui.
              </div>
            </>
          )}

          {fase === 'ok' && (
            <div className={e.conectou}>
              {miniaturaBase64 && (
                <div className={e.miniatura}>
                  <img
                    className={e.miniaturaImg}
                    alt="Imagem de teste recebida da câmera"
                    src={`data:image/jpeg;base64,${miniaturaBase64}`}
                    onLoad={(ev) => setDimensoes({ largura: ev.currentTarget.naturalWidth, altura: ev.currentTarget.naturalHeight })}
                  />
                  <span className={e.carimboMiniatura}>{lente ? `Lente ${lente} · agora` : 'Agora'}</span>
                </div>
              )}
              <div style={{ flex: 1, minWidth: 200 }}>
                <div className={mergeClasses(tipo.subtitulo, e.conectouTitulo)}>
                  <CheckmarkCircle24Filled aria-hidden="true" /> Conectou
                </div>
                <div style={{ marginTop: 4 }}>
                  {lente ? `Recebemos uma imagem da lente ${lente}.` : 'Recebemos uma imagem da câmera.'}
                </div>
                <div className={mergeClasses(tipo.legenda, e.dica)}>
                  {dimensoes ? `${dimensoes.largura} × ${dimensoes.altura} · ` : ''}qualidade {qualidade}
                </div>
                <div className={mergeClasses(tipo.legenda, e.dica)}>
                  A imagem do teste é só para conferir o enquadramento. Ela não fica guardada.
                </div>
              </div>
            </div>
          )}

          {fase === 'falha' && falha && (
            <FeedbackInline
              tom="aviso"
              acao={{
                rotulo: 'Tentar de novo',
                aoClicar: () => {
                  setDimensoes(null);
                  void iniciar(solicitar);
                },
              }}
            >
              <strong>{falha.titulo}</strong>
              <br />
              {falha.texto}
              {falha.detalhe && (
                <>
                  <br />
                  <span className={tipo.legenda}>Detalhe: {falha.detalhe}</span>
                </>
              )}
            </FeedbackInline>
          )}
        </div>
      </div>
    </div>
  );
}

function Passo({ estado, numero, children }: { estado: 'feito' | 'andamento' | 'pendente'; numero: number; children: string }) {
  const e = useEstilosCameraIp();
  return (
    <div className={mergeClasses(e.passo, estado === 'pendente' && e.passoPendente)}>
      {estado === 'feito' && (
        <span aria-hidden="true" className={mergeClasses(e.passoBolinha, e.passoFeito)}>
          <Checkmark16Regular />
        </span>
      )}
      {estado === 'andamento' && <Spinner size="extra-tiny" aria-hidden="true" />}
      {estado === 'pendente' && (
        <span aria-hidden="true" className={mergeClasses(e.passoBolinha, e.passoAberto)}>
          {numero}
        </span>
      )}
      {children}
    </div>
  );
}
