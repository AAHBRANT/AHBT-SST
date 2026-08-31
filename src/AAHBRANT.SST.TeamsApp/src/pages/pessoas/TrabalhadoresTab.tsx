import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Avatar, Badge, Button, Field, Input, Select, Text } from '@fluentui/react-components';
import { Add24Regular, ChevronRight24Regular, Delete24Regular, Fingerprint24Regular, Search24Regular } from '@fluentui/react-icons';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  tipoVinculoLabel,
  TipoVinculo,
  type Aso,
  type Funcao,
  type NovoTrabalhador,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { formatarCpf } from '../../lib/cpf';
import { usePageStyles } from '../pageStyles';
import { CadastroDigitalDialog } from './CadastroDigitalDialog';

function corBadgeAso(status: number | undefined): 'success' | 'warning' | 'danger' | 'informative' {
  switch (status) {
    case ResultadoAso.Apto:
      return 'success';
    case ResultadoAso.AptoComRestricao:
      return 'warning';
    case ResultadoAso.Inapto:
      return 'danger';
    default:
      return 'informative';
  }
}

function ultimoAso(asos: Aso[], trabalhadorId: string): Aso | undefined {
  return asos
    .filter((a) => a.trabalhadorId === trabalhadorId)
    .sort((a, b) => new Date(b.dataExame).getTime() - new Date(a.dataExame).getTime())[0];
}

const trabalhadorVazio: NovoTrabalhador = {
  obraId: '',
  setorId: null,
  equipeId: null,
  funcaoId: '',
  nome: '',
  matricula: '',
  cpf: '',
  vinculo: TipoVinculo.Clt,
  dataAdmissao: '',
  turno: '',
};

export function TrabalhadoresTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [busca, setBusca] = useState('');
  const [novoTrabalhador, setNovoTrabalhador] = useState<NovoTrabalhador>(trabalhadorVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});
  const [trabalhadorDigitalAlvo, setTrabalhadorDigitalAlvo] = useState<{ id: string; nome: string } | null>(
    null,
  );

  async function carregar() {
    try {
      setErro(null);
      const [trabs, obs, funcs, asosResp] = await Promise.all([
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.funcoes.listar(),
        api.asos.listar(),
      ]);
      setTrabalhadores(trabs);
      setObras(obs);
      setFuncoes(funcs);
      setAsos(asosResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar trabalhadores.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Miniaturas da foto são baixadas sob demanda (só para trabalhadores com temFoto) e mantidas como
  // object URL até a página ser desmontada — mesmo padrão de ObrasPage.baixarLogo.
  useEffect(() => {
    let cancelado = false;
    (async () => {
      for (const trabalhador of trabalhadores) {
        if (!trabalhador.temFoto || fotoUrls[trabalhador.id]) continue;
        try {
          const blob = await api.trabalhadores.baixarFoto(trabalhador.id);
          if (cancelado) return;
          setFotoUrls((atual) => ({ ...atual, [trabalhador.id]: URL.createObjectURL(blob) }));
        } catch {
          // Falha ao carregar miniatura não impede o uso da página; o trabalhador fica sem foto.
        }
      }
    })();
    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadores]);

  useEffect(() => {
    return () => {
      Object.values(fotoUrls).forEach((url) => URL.revokeObjectURL(url));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function enviarFoto(trabalhadorId: string, arquivo: File) {
    try {
      setErro(null);
      await api.trabalhadores.enviarFoto(trabalhadorId, arquivo);
      setFotoUrls((atual) => {
        const anterior = atual[trabalhadorId];
        if (anterior) URL.revokeObjectURL(anterior);
        const { [trabalhadorId]: _removido, ...resto } = atual;
        return resto;
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto.');
    }
  }

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  function nomeFuncao(id: string) {
    return funcoes.find((f) => f.id === id)?.nome ?? id;
  }

  const trabalhadoresFiltrados = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    if (!termo) return trabalhadores;
    return trabalhadores.filter(
      (t) => t.nome.toLowerCase().includes(termo) || t.matricula.toLowerCase().includes(termo),
    );
  }, [busca, trabalhadores]);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.trabalhadores.criar(novoTrabalhador);
      setTrabalhadorDigitalAlvo({ id, nome: novoTrabalhador.nome });
      setNovoTrabalhador(trabalhadorVazio);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar trabalhador.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    try {
      await api.trabalhadores.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir trabalhador.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Trabalhadores cadastrados</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Obra">
          <Select
            value={novoTrabalhador.obraId}
            onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, obraId: d.value })}
          >
            <option value="">Selecione</option>
            {obras.map((obra) => (
              <option key={obra.id} value={obra.id}>
                {obra.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Função">
          <Select
            value={novoTrabalhador.funcaoId}
            onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, funcaoId: d.value })}
          >
            <option value="">Selecione</option>
            {funcoes.map((funcao) => (
              <option key={funcao.id} value={funcao.id}>
                {funcao.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Nome">
          <Input
            value={novoTrabalhador.nome}
            onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, nome: d.value })}
          />
        </Field>
        <Field label="Matrícula">
          <Input
            value={novoTrabalhador.matricula}
            onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, matricula: d.value })}
          />
        </Field>
        <Field label="CPF (11 dígitos)">
          <Input
            value={formatarCpf(novoTrabalhador.cpf)}
            onChange={(_, d) =>
              setNovoTrabalhador({ ...novoTrabalhador, cpf: d.value.replace(/\D/g, '').slice(0, 11) })
            }
          />
        </Field>
        <Field label="Vínculo">
          <Select
            value={novoTrabalhador.vinculo}
            onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, vinculo: Number(d.value) })}
          >
            {Object.entries(tipoVinculoLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Data de admissão">
          <Input
            type="date"
            value={novoTrabalhador.dataAdmissao}
            onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, dataAdmissao: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar trabalhador
        </Button>
      </div>

      <Field label="Buscar por nome ou matrícula" style={{ marginBottom: 12 }}>
        <Input contentBefore={<Search24Regular />} value={busca} onChange={(_, d) => setBusca(d.value)} />
      </Field>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        {trabalhadoresFiltrados.map((trabalhador) => {
          const aso = ultimoAso(asos, trabalhador.id);
          return (
            <div
              key={trabalhador.id}
              onClick={() => navigate(`/pessoas/${trabalhador.id}`)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                padding: '10px 8px',
                borderRadius: 8,
                cursor: 'pointer',
              }}
            >
              {fotoUrls[trabalhador.id] ? (
                <Avatar image={{ src: fotoUrls[trabalhador.id] }} size={40} name={trabalhador.nome} />
              ) : (
                <Avatar name={trabalhador.nome} color="colorful" size={40} />
              )}
              <div style={{ flexGrow: 1, minWidth: 0 }}>
                <Text weight="semibold" block truncate>
                  {trabalhador.nome}
                </Text>
                <Text size={200}>
                  {trabalhador.matricula} · {nomeObra(trabalhador.obraId)} · {nomeFuncao(trabalhador.funcaoId)} ·{' '}
                  {tipoVinculoLabel[trabalhador.vinculo]}
                </Text>
              </div>
              {aso && (
                <Badge color={corBadgeAso(aso.resultadoStatus)} appearance="tint">
                  {resultadoAsoLabel[aso.resultadoStatus]}
                </Badge>
              )}
              {!trabalhador.temBiometria && (
                <Badge color="warning" appearance="tint">
                  Digital pendente
                </Badge>
              )}
              <div style={{ display: 'flex', gap: 4 }}>
                <Button
                  appearance="subtle"
                  icon={<Fingerprint24Regular />}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    setTrabalhadorDigitalAlvo({ id: trabalhador.id, nome: trabalhador.nome });
                  }}
                  aria-label="Cadastrar digital"
                  title="Cadastrar digital"
                />
                <span onClick={(evento) => evento.stopPropagation()}>
                  <SeletorFotoCamera
                    rotulo="Enviar foto"
                    apenasIcone
                    tiposAceitos="image/png,image/jpeg"
                    aoSelecionarArquivo={(arquivo) => enviarFoto(trabalhador.id, arquivo)}
                  />
                </span>
                <Button
                  appearance="subtle"
                  icon={<ChevronRight24Regular />}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    navigate(`/pessoas/${trabalhador.id}`);
                  }}
                  aria-label="Ver perfil"
                />
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={(evento) => excluir(trabalhador.id, evento)}
                  aria-label="Excluir"
                />
              </div>
            </div>
          );
        })}
        {trabalhadoresFiltrados.length === 0 && <Text>Nenhum trabalhador encontrado.</Text>}
      </div>

      <CadastroDigitalDialog
        trabalhadorId={trabalhadorDigitalAlvo?.id ?? null}
        trabalhadorNome={trabalhadorDigitalAlvo?.nome}
        aoFechar={() => setTrabalhadorDigitalAlvo(null)}
        aoConcluir={carregar}
      />
    </div>
  );
}
