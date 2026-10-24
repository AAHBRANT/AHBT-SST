import { useState } from 'react';
import { Button, Card, FeedbackInline, Legenda, useConfirmar } from '@ui';
import { CloudArrowUp24Regular } from '@fluentui/react-icons';
import {
  api,
  type ImportarAlojamentosGrhResultado,
  type ImportarAsoGrhResultado,
  type ImportarColaboradoresGrhResultado,
} from '../../lib/api';

// Carga inicial única do cadastro do G-RH (Integração G-RH, 2026-09-09) — dispara
// ImportarColaboradoresGrhCommand. A atualização contínua depois disso é automática, via evento de
// Service Bus (ServiceBusColaboradorGrhProcessor); repetir esta importação não é o fluxo esperado,
// mas é seguro (upsert por CPF, nunca duplica).
export function IntegracaoGrhTab() {
  return (
    <>
      <CardImportarColaboradores />
      <div style={{ marginTop: 16 }}>
        <CardImportarAlojamentos />
      </div>
      <div style={{ marginTop: 16 }}>
        <CardImportarAsos />
      </div>
    </>
  );
}

function CardImportarColaboradores() {
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
      titulo="Integração G-RH — Colaboradores"
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

// Carga inicial única do cadastro de alojamentos do G-RH (Integração G-RH, 2026-09-16) — dispara
// ImportarAlojamentosGrhCommand. O alojamento é cadastrado exclusivamente pelo G-RH; a atualização
// contínua depois desta carga é automática, via evento de Service Bus
// (ServiceBusAlojamentoGrhProcessor); repetir esta importação não é o fluxo esperado, mas é seguro
// (upsert por GrhAlojamentoId, nunca duplica).
function CardImportarAlojamentos() {
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ImportarAlojamentosGrhResultado | null>(null);
  const { confirmar, dialogElement } = useConfirmar();

  async function importar() {
    const confirmou = await confirmar({
      titulo: 'Importar alojamentos do G-RH',
      mensagem:
        'Isso busca todo o cadastro de alojamentos no G-RH e cria/atualiza os alojamentos e seus moradores no SST. Pode levar alguns segundos. Confirma?',
      rotuloConfirmar: 'Importar',
    });
    if (!confirmou) return;

    try {
      setCarregando(true);
      setErro(null);
      setResultado(null);
      const res = await api.alojamentos.importarGrh();
      setResultado(res);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao importar alojamentos do G-RH.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <Card
      titulo="Integração G-RH — Alojamentos"
      subtitulo="Carga inicial do cadastro de alojamentos vindo do G-RH. O G-RH é a fonte única de verdade para estes dados — o alojamento nunca é cadastrado manualmente no SST."
    >
      {dialogElement}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Button appearance="primary" icon={<CloudArrowUp24Regular />} onClick={importar} disabled={carregando}>
        {carregando ? 'Importando…' : 'Importar alojamentos do G-RH'}
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

// Sincronização de ASO lida direto do banco do G-RH (Integração G-RH, 2026-09-16) — diferente de
// Colaborador/Alojamento, aqui não há endpoint HTTP nem fila: um serviço de polling
// (GrhDbPollingService) já roda essa mesma importação automaticamente a cada alguns minutos. Este
// botão só força uma rodada manual, útil pra não esperar o próximo ciclo. ResultadoStatus/
// restrições/médico só são atualizados quando o G-RH manda valor preenchido — nunca apagam uma
// avaliação clínica já lançada manualmente no SST.
function CardImportarAsos() {
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ImportarAsoGrhResultado | null>(null);
  const { confirmar, dialogElement } = useConfirmar();

  async function importar() {
    const confirmou = await confirmar({
      titulo: 'Importar ASOs do G-RH',
      mensagem:
        'Isso busca os ASOs do G-RH e cria/atualiza os registros correspondentes no SST (por CPF). ASOs ainda sem validade preenchida no G-RH são pulados até lá. Confirma?',
      rotuloConfirmar: 'Importar',
    });
    if (!confirmou) return;

    try {
      setCarregando(true);
      setErro(null);
      setResultado(null);
      const res = await api.asos.importarGrh();
      setResultado(res);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao importar ASOs do G-RH.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <Card
      titulo="Integração G-RH — ASO"
      subtitulo="Sincronização automática (a cada alguns minutos) via leitura direta do banco do G-RH. Este botão força uma rodada agora."
    >
      {dialogElement}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Button appearance="primary" icon={<CloudArrowUp24Regular />} onClick={importar} disabled={carregando}>
        {carregando ? 'Importando…' : 'Importar ASOs do G-RH agora'}
      </Button>

      {resultado && (
        <div style={{ marginTop: 12 }}>
          <Legenda>
            {resultado.totalRecebidos} recebido(s) do G-RH · {resultado.totalSincronizados} sincronizado(s) com
            sucesso
            {resultado.erros.length > 0 && ` · ${resultado.erros.length} ainda sem validade ou com erro`}
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
