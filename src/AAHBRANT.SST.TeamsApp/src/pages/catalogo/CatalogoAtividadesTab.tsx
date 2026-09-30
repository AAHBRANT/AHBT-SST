import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  Input,
  Select,
  StatusChip,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  nivelRiscoLabel,
  statusAprLabel,
  statusPtLabel,
  type CatalogoAtividade,
  type CatalogoDocumento,
  type Equipe,
  type Obra,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { hojeIso } from '../../lib/datas';

type TipoCatalogo = 'apr' | 'pt';

const tomPorNivel: Record<number, Tom> = { 1: 'neutro', 2: 'info', 3: 'atencao', 4: 'alerta', 5: 'alerta' };

// Catálogo por atividade da obra (29/09), mesma cadeia do PGR: Atividade → Riscos avaliados. A
// "lista de modelos" é a lista de atividades; "Gerar" cria a APR/PT em elaboração já preenchida com
// os riscos do PGR, para revisar e completar. Serve às duas abas (APR e PT), só muda `tipo`.
export function CatalogoAtividadesTab({ tipo }: { tipo: TipoCatalogo }) {
  const navigate = useNavigate();
  const sucessoToast = useSucessoToast();
  const rotulo = tipo === 'apr' ? 'APR' : 'PT';
  const rotuloStatus = tipo === 'apr' ? statusAprLabel : statusPtLabel;
  const caminhoBase = tipo === 'apr' ? '/operacao/apr' : '/operacao/pt';

  const [linhas, setLinhas] = useState<CatalogoAtividade[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [obraId, setObraId] = useState('');
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [selecionada, setSelecionada] = useState<CatalogoAtividade | null>(null);
  const [local, setLocal] = useState('');
  const [equipeId, setEquipeId] = useState('');
  const [data, setData] = useState(hojeIso());
  const [validade, setValidade] = useState('');
  const [gerando, setGerando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setCarregando(true);
      const [lista, listaObras, listaEquipes] = await Promise.all([
        api.catalogo.atividades(obraId || undefined),
        api.obras.listar(),
        api.equipes.listar(),
      ]);
      setLinhas(lista);
      setObras(listaObras);
      setEquipes(listaEquipes);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar o catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    void carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obraId]);

  // O painel de geração abre abaixo da tabela; sem rolar, quem clica no "+" de uma linha do topo
  // não vê nada acontecer.
  const painelRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (selecionada) painelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [selecionada]);

  const documentos = (a: CatalogoAtividade): CatalogoDocumento[] => (tipo === 'apr' ? a.aprs : a.pts);
  const equipesDaObra = useMemo(
    () => equipes.filter((e) => !selecionada || e.obraId === selecionada.obraId),
    [equipes, selecionada],
  );

  function escolher(a: CatalogoAtividade) {
    setSelecionada(a);
    setEquipeId('');
    setValidade('');
    setErro(null);
  }

  async function gerar() {
    if (!selecionada) return;
    try {
      setGerando(true);
      setErro(null);
      const dados = {
        atividadeId: selecionada.atividadeId,
        local: local.trim(),
        equipeId: equipeId || null,
        data,
        validade: validade || null,
      };
      const { id } = tipo === 'apr' ? await api.catalogo.gerarApr(dados) : await api.catalogo.gerarPt(dados);
      sucessoToast(`${rotulo} gerada em elaboração. Revise antes de ${tipo === 'apr' ? 'aprovar' : 'liberar'}.`);
      navigate(`${caminhoBase}/${id}`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : `Falha ao gerar ${rotulo}.`);
    } finally {
      setGerando(false);
    }
  }

  const colunas: Coluna<CatalogoAtividade>[] = [
    { chave: 'nome', rotulo: 'Atividade' },
    { chave: 'obraNome', rotulo: 'Obra' },
    {
      chave: 'quantidadeRiscos',
      rotulo: 'Riscos no PGR',
      render: (a) =>
        a.quantidadeRiscos === 0 ? (
          <StatusChip tom="atencao">Sem riscos</StatusChip>
        ) : (
          <span>
            {a.quantidadeRiscos}{' '}
            {a.maiorNivelRisco != null && (
              <StatusChip tom={tomPorNivel[a.maiorNivelRisco] ?? 'neutro'}>
                {nivelRiscoLabel[a.maiorNivelRisco]}
              </StatusChip>
            )}
          </span>
        ),
    },
    {
      chave: 'emitidas',
      rotulo: `${rotulo}s emitidas`,
      render: (a) => documentos(a).length,
    },
    {
      chave: 'vigente',
      rotulo: 'Situação',
      render: (a) =>
        documentos(a).some((d) => d.vigente) ? (
          <StatusChip tom="ok">Vigente</StatusChip>
        ) : (
          <StatusChip tom="alerta">Sem {rotulo} vigente</StatusChip>
        ),
    },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo={`Catálogo de ${rotulo} por atividade`}>
        <FormGrid>
          <Campo span={4}>
            <Field label="Obra">
              <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
                <option value="">Todas as obras</option>
                {obras.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
        </FormGrid>

        <DataTable
          aria-label={`Catálogo de ${rotulo}`}
          colunas={colunas}
          linhas={linhas}
          chaveLinha={(a) => a.atividadeId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhuma atividade cadastrada para esta obra.' }}
          aoClicarLinha={escolher}
          acoesLinha={(a) => (
            <BotaoAcao
              tom="ver"
              icon={<Add24Regular />}
              onClick={() => escolher(a)}
              aria-label={`Gerar ${rotulo} desta atividade`}
            />
          )}
        />
      </Card>

      {selecionada && (
        <div ref={painelRef}>
        <Card titulo={selecionada.nome}>
          <p style={{ marginTop: 0 }}>
            {selecionada.obraNome}
            {selecionada.pgrNome ? ` — PGR: ${selecionada.pgrNome}` : ' — obra sem PGR cadastrado'}
          </p>

          {documentos(selecionada).length > 0 && (
            <DataTable
              aria-label={`${rotulo}s emitidas para a atividade`}
              colunas={[
                { chave: 'numero', rotulo: `Nº ${rotulo}`, render: (d: CatalogoDocumento) => d.numero ?? '-' },
                { chave: 'data', rotulo: 'Data', render: (d: CatalogoDocumento) => d.data.slice(0, 10) },
                { chave: 'validade', rotulo: 'Validade', render: (d: CatalogoDocumento) => d.validade?.slice(0, 10) ?? '' },
                {
                  chave: 'status',
                  rotulo: 'Status',
                  render: (d: CatalogoDocumento) => (
                    <StatusChip tom={d.vigente ? 'ok' : 'neutro'}>{rotuloStatus[d.status]}</StatusChip>
                  ),
                },
              ]}
              linhas={documentos(selecionada)}
              chaveLinha={(d) => d.id}
              vazio={{ titulo: 'Nenhuma emitida.' }}
              aoClicarLinha={(d) => navigate(`${caminhoBase}/${d.id}`)}
            />
          )}

          <h4>Gerar {rotulo} a partir dos riscos do PGR</h4>
          {selecionada.quantidadeRiscos === 0 ? (
            <FeedbackInline tom="erro">
              Esta atividade ainda não tem riscos avaliados no PGR. Cadastre os riscos antes de gerar a {rotulo}.
            </FeedbackInline>
          ) : (
            <>
              <FormGrid>
                <Campo span={4}>
                  <Field label="Local / Frente" required>
                    <Input value={local} onChange={(_, d) => setLocal(d.value)} />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Equipe">
                    <Select value={equipeId} onChange={(_, d) => setEquipeId(d.value)}>
                      <option value="">Nenhuma</option>
                      {equipesDaObra.map((e) => (
                        <option key={e.id} value={e.id}>
                          {e.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label="Data">
                    <CampoData value={data} onChange={(_, d) => setData(d.value)} />
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label="Validade">
                    <CampoData value={validade} onChange={(_, d) => setValidade(d.value)} />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button appearance="primary" icon={<Add24Regular />} onClick={gerar} disabled={gerando || !local.trim()}>
                  Gerar {rotulo}
                </Button>
                <Button onClick={() => setSelecionada(null)} disabled={gerando}>
                  Fechar
                </Button>
              </FormRodape>
            </>
          )}
        </Card>
        </div>
      )}
    </>
  );
}
