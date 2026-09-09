import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  Field,
  FeedbackInline,
  FormGrid,
  FormSection,
  Legenda,
  Select,
  Textarea,
  useConfirmar,
} from '@ui';
import { CloudArrowUp24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type ImportarRiscosLoteResultado, type Obra, type RiscoLoteItem } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const EXEMPLO = `[
  {
    "nomeAtividade": "Alvenaria e serviços gerais de canteiro (Pedreiro / Ajudante-Servente)",
    "descricaoAtividade": "GHE 01. Funções: Pedreiro, Ajudante de Obras.",
    "nomePerigo": "Ruído do ambiente",
    "agentePerigo": "Físico",
    "ambiente": "Estrutura com paredes de madeira, piso de concreto, iluminação natural, climatização natural e atividades a céu aberto.",
    "exposicao": "Qualitativa",
    "consequencia": "Redução da capacidade auditiva, zumbido, irritabilidade.",
    "probabilidade": 1,
    "severidade": 3,
    "controlesExistentes": "EPC/MA: Placas de sinalização | EPI: Protetor auricular tipo concha",
    "controlesAdicionais": "Será avaliado através de avaliação qualitativa (dosimetria) e LTCAT"
  }
]`;

// Complementa o cadastro manual (item a item) de Atividade/Perigo/Risco: útil para transcrever um
// inventário de risco inteiro (ex.: PGR de uma obra) de uma vez, sem precisar passar pela tela de
// cadastro registro por registro. Atividade e Perigo são resolvidos por nome — criados
// automaticamente se ainda não existirem, reaproveitados se já existirem.
// Camada ui/ (Onda 2, Task 13): sem lista para empurrar pra baixo, então sem PainelLateral — só
// embrulha em Card + FormSection/FormGrid/Campo para o seletor de Obra.
export function ImportarLoteTab() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [json, setJson] = useState('');
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ImportarRiscosLoteResultado | null>(null);
  const [limpando, setLimpando] = useState(false);
  const sucessoToast = useSucessoToast();
  const { confirmar, dialogElement } = useConfirmar();

  useEffect(() => {
    api.obras.listar().then(setObras).catch(() => setErro('Falha ao carregar obras.'));
  }, []);

  async function importar() {
    setErro(null);
    setResultado(null);
    if (!obraId) {
      setErro('Selecione a obra.');
      return;
    }
    let itens: RiscoLoteItem[];
    try {
      itens = JSON.parse(json);
      if (!Array.isArray(itens) || itens.length === 0) throw new Error('empty');
    } catch {
      setErro('JSON inválido — cole um array de itens conforme o exemplo abaixo.');
      return;
    }
    try {
      setCarregando(true);
      const res = await api.riscos.importarLote(obraId, itens);
      setResultado(res);
      sucessoToast(`Importação concluída: ${res.riscosCriados} riscos criados.`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao importar lote.');
    } finally {
      setCarregando(false);
    }
  }

  async function limparRiscosDaObra() {
    if (!obraId) {
      setErro('Selecione a obra.');
      return;
    }
    const nomeObra = obras.find((o) => o.id === obraId)?.nome ?? obraId;
    const confirmou = await confirmar({
      titulo: 'Limpar riscos da obra',
      mensagem: `Isso vai apagar TODAS as avaliações de risco já cadastradas para "${nomeObra}" (ex.: após uma importação duplicada por engano). Atividades e Perigos cadastrados não são afetados. Essa ação não pode ser desfeita. Confirma?`,
      rotuloConfirmar: 'Apagar todos os riscos',
    });
    if (!confirmou) return;
    try {
      setLimpando(true);
      setErro(null);
      const res = await api.riscos.limparPorObra(obraId);
      setResultado(null);
      sucessoToast(`${res.riscosRemovidos} risco(s) removido(s) de "${nomeObra}".`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao limpar riscos da obra.');
    } finally {
      setLimpando(false);
    }
  }

  return (
    <Card
      titulo="Importar riscos em lote"
      subtitulo="Cole um array JSON de riscos (ex.: transcrito de um PGR). Atividade e Perigo são criados automaticamente se ainda não existirem para a obra selecionada, ou reaproveitados se já existirem."
    >
      {dialogElement}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <FormSection titulo="Seleção da Obra" primeira>
        <FormGrid>
          <Campo span={4}>
            <Field label="Obra">
              <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
                <option value="">Selecione</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
        </FormGrid>
      </FormSection>

      <Field label="Itens (JSON)">
        <Textarea value={json} onChange={(_, d) => setJson(d.value)} rows={12} placeholder={EXEMPLO} />
      </Field>

      <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
        <Button appearance="primary" icon={<CloudArrowUp24Regular />} onClick={importar} disabled={carregando}>
          Importar lote
        </Button>
        <Button
          appearance="outline"
          icon={<Delete24Regular />}
          onClick={limparRiscosDaObra}
          disabled={limpando || !obraId}
        >
          Limpar riscos desta obra
        </Button>
      </div>

      {resultado && (
        <div style={{ marginTop: 12 }}>
          <Legenda>
            {resultado.atividadesCriadas} atividade(s) nova(s), {resultado.perigosCriados} perigo(s) novo(s),{' '}
            {resultado.riscosCriados} risco(s) criado(s).
          </Legenda>
        </div>
      )}
    </Card>
  );
}
