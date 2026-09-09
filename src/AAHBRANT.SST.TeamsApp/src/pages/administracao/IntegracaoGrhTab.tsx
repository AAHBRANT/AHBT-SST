import { useState } from 'react';
import { Button, Card, FeedbackInline, Legenda, useConfirmar } from '@ui';
import { CloudArrowUp24Regular } from '@fluentui/react-icons';
import { api, type ImportarColaboradoresGrhResultado } from '../../lib/api';

// Carga inicial única do cadastro do G-RH (Integração G-RH, 2026-09-09) — dispara
// ImportarColaboradoresGrhCommand. A atualização contínua depois disso é automática, via evento de
// Service Bus (ServiceBusColaboradorGrhProcessor); repetir esta importação não é o fluxo esperado,
// mas é seguro (upsert por CPF, nunca duplica).
export function IntegracaoGrhTab() {
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ImportarColaboradoresGrhResultado | null>(null);
  const { confirmar, dialogElement } = useConfirmar();

  async function importar() {
    const confirmou = await confirmar({
      titulo: 'Importar colaboradores do G-RH',
      mensagem:
        'Isso busca todo o cadastro de colaboradores no G-RH e cria/atualiza os trabalhadores correspondentes no SST (por CPF). Pode levar alguns segundos. Confirma?',
      rotuloConfirmar: 'Importar',
    });
    if (!confirmou) return;

    try {
      setCarregando(true);
      setErro(null);
      setResultado(null);
      const res = await api.trabalhadores.importarGrh();
      setResultado(res);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao importar colaboradores do G-RH.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <Card
      titulo="Integração G-RH"
      subtitulo="Carga inicial do cadastro de colaboradores vindo do G-RH. O G-RH é a fonte única de verdade para estes dados — o SST só reflete."
    >
      {dialogElement}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Button appearance="primary" icon={<CloudArrowUp24Regular />} onClick={importar} disabled={carregando}>
        {carregando ? 'Importando…' : 'Importar colaboradores do G-RH'}
      </Button>

      {resultado && (
        <div style={{ marginTop: 12 }}>
          <Legenda>
            {resultado.totalRecebidos} recebido(s) do G-RH · {resultado.totalSincronizados} sincronizado(s) com
            sucesso
            {resultado.erros.length > 0 && ` · ${resultado.erros.length} com erro`}
          </Legenda>
          {resultado.erros.length > 0 && (
            <ul style={{ marginTop: 8, paddingLeft: 20, fontSize: 12 }}>
              {resultado.erros.map((mensagemErro, indice) => (
                <li key={indice}>{mensagemErro}</li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Card>
  );
}
