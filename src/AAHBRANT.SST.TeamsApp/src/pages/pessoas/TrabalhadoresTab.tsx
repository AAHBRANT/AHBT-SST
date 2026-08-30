import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Add24Regular, ChevronRight24Regular, Delete24Regular, PeopleList24Regular } from '@fluentui/react-icons';
import {
  api,
  tipoVinculoLabel,
  TipoVinculo,
  type Funcao,
  type NovoTrabalhador,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { formatarCpf } from '../../lib/cpf';
import { usePageStyles } from '../pageStyles';
import { TrabalhadoresGaveta } from './TrabalhadoresGaveta';

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
  const [novoTrabalhador, setNovoTrabalhador] = useState<NovoTrabalhador>(trabalhadorVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [gavetaAberta, setGavetaAberta] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [trabs, obs, funcs] = await Promise.all([
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.funcoes.listar(),
      ]);
      setTrabalhadores(trabs);
      setObras(obs);
      setFuncoes(funcs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar trabalhadores.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  function nomeFuncao(id: string) {
    return funcoes.find((f) => f.id === id)?.nome ?? id;
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.trabalhadores.criar(novoTrabalhador);
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
        <Button appearance="secondary" icon={<PeopleList24Regular />} onClick={() => setGavetaAberta(true)}>
          Ver com fotos
        </Button>
      </div>

      <TrabalhadoresGaveta
        aberta={gavetaAberta}
        aoFechar={() => setGavetaAberta(false)}
        trabalhadores={trabalhadores}
      />

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
        <Field label="Turno">
          <Input
            value={novoTrabalhador.turno ?? ''}
            onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, turno: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar trabalhador
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Matrícula</TableHeaderCell>
            <TableHeaderCell>Obra</TableHeaderCell>
            <TableHeaderCell>Função</TableHeaderCell>
            <TableHeaderCell>Vínculo</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {trabalhadores.map((trabalhador) => (
            <TableRow
              key={trabalhador.id}
              onClick={() => navigate(`/operacao/pessoas/${trabalhador.id}`)}
              style={{ cursor: 'pointer' }}
            >
              <TableCell>{trabalhador.nome}</TableCell>
              <TableCell>{trabalhador.matricula}</TableCell>
              <TableCell>{nomeObra(trabalhador.obraId)}</TableCell>
              <TableCell>{nomeFuncao(trabalhador.funcaoId)}</TableCell>
              <TableCell>{tipoVinculoLabel[trabalhador.vinculo]}</TableCell>
              <TableCell>
                <div style={{ display: 'flex', gap: 4 }}>
                  <Button
                    appearance="subtle"
                    icon={<ChevronRight24Regular />}
                    onClick={() => navigate(`/operacao/pessoas/${trabalhador.id}`)}
                    aria-label="Ver perfil"
                  />
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(evento) => excluir(trabalhador.id, evento)}
                    aria-label="Excluir"
                  />
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
