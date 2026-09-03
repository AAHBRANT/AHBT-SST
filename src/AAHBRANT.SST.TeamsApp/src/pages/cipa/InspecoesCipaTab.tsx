import { useEffect, useState } from 'react';
import {
  Badge,
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
  Textarea,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { Add24Regular, Delete24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  api,
  nivelRiscoLabel,
  type InspecaoCipa,
  type MembroCipa,
  type NovaInspecaoCipa,
  type Obra,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function vazio(): NovaInspecaoCipa {
  return { obraId: '', membroCipaId: null, data: '', local: '', riscoIdentificado: '', grauRisco: null };
}

// Integração com PGR/GRO: este sistema NÃO envia alertas automáticos ao inventário de riscos do
// GRO. O botão "Gerar Não Conformidade" cria manualmente uma Não Conformidade (mesmo mecanismo de
// NaoConformidadesTab.tsx) a partir do risco identificado na inspeção — ver disclosure em Cipa.cs.
export function InspecoesCipaTab() {
  const estilos = usePageStyles();
  const [lista, setLista] = useState<InspecaoCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [membros, setMembros] = useState<MembroCipa[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novo, setNovo] = useState<NovaInspecaoCipa>(vazio());
  const [gerandoNcPara, setGerandoNcPara] = useState<string | null>(null);
  const [responsavelNc, setResponsavelNc] = useState('');
  const [prazoNc, setPrazoNc] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaInspecoes, listaObras, listaUsuarios] = await Promise.all([
        api.cipa.inspecoes.listar(),
        api.obras.listar(),
        api.usuarios.listar(),
      ]);
      setLista(listaInspecoes);
      setObras(listaObras);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar inspeções da CIPA.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  async function trocarObra(obraId: string) {
    setNovo({ ...novo, obraId, membroCipaId: null });
    setMembros(obraId ? await api.cipa.membros.listar(obraId, true) : []);
  }

  async function criar() {
    if (!novo.obraId || !novo.data || !novo.local.trim() || !novo.riscoIdentificado.trim()) {
      setErro('Preencha obra, data, local e o risco identificado.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.cipa.inspecoes.criar(novo);
      setNovo(vazio());
      setMembros([]);
      await carregar();
      sucessoToast('Inspeção registrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar inspeção.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta inspeção? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.inspecoes.excluir(id);
      await carregar();
      sucessoToast('Inspeção excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir inspeção.');
    }
  }

  async function confirmarGerarNc() {
    if (!gerandoNcPara) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.cipa.inspecoes.gerarNaoConformidade(gerandoNcPara, responsavelNc || null, prazoNc || null);
      setGerandoNcPara(null);
      setResponsavelNc('');
      setPrazoNc('');
      await carregar();
      sucessoToast('Não conformidade gerada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar não conformidade.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova inspeção</Text>
        </div>
        {erro && <Text className={estilos.erro}>{erro}</Text>}
        <div className={estilos.form}>
          <Field label="Obra" required>
            <Select value={novo.obraId} onChange={(_, d) => trocarObra(d.value)}>
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Membro que inspecionou">
            <Select
              value={novo.membroCipaId ?? ''}
              onChange={(_, d) => setNovo({ ...novo, membroCipaId: d.value || null })}
              disabled={!novo.obraId}
            >
              <option value="">Não informado</option>
              {membros.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.trabalhadorNome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Data" required>
            <CampoData value={novo.data} onChange={(_, d) => setNovo({ ...novo, data: d.value })} />
          </Field>
          <Field label="Local" required>
            <Input value={novo.local} onChange={(_, d) => setNovo({ ...novo, local: d.value })} />
          </Field>
          <Field label="Grau de risco">
            <Select
              value={novo.grauRisco != null ? String(novo.grauRisco) : ''}
              onChange={(_, d) => setNovo({ ...novo, grauRisco: d.value ? Number(d.value) : null })}
            >
              <option value="">Não informado</option>
              {Object.entries(nivelRiscoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Risco identificado" required>
            <Textarea value={novo.riscoIdentificado} onChange={(_, d) => setNovo({ ...novo, riscoIdentificado: d.value })} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Registrar inspeção
          </Button>
        </div>
      </div>

      {gerandoNcPara && (
        <div className={estilos.card} style={{ marginBottom: 16 }}>
          <div className={estilos.toolbar}>
            <Text weight="semibold">Gerar não conformidade</Text>
          </div>
          <div className={estilos.form}>
            <Field label="Responsável">
              <Select value={responsavelNc} onChange={(_, d) => setResponsavelNc(d.value)}>
                <option value="">Nenhum</option>
                {usuarios.map((usuario) => (
                  <option key={usuario.id} value={usuario.id}>
                    {usuario.nome}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Prazo">
              <CampoData value={prazoNc} onChange={(_, d) => setPrazoNc(d.value)} />
            </Field>
          </div>
          <div className={estilos.formActions}>
            <Button appearance="secondary" onClick={() => setGerandoNcPara(null)}>
              Cancelar
            </Button>
            <Button appearance="primary" onClick={confirmarGerarNc} disabled={carregando}>
              Confirmar
            </Button>
          </div>
        </div>
      )}

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Inspeções registradas</Text>
        </div>
        {carregandoLista ? (
          <ListaCarregando />
        ) : lista.length === 0 ? (
          <EstadoVazio mensagem="Nenhuma inspeção registrada ainda." />
        ) : (
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Data</TableHeaderCell>
              <TableHeaderCell>Local</TableHeaderCell>
              <TableHeaderCell>Risco identificado</TableHeaderCell>
              <TableHeaderCell>Grau</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {lista.map((i) => (
              <TableRow key={i.id}>
                <TableCell>{nomeObra(i.obraId)}</TableCell>
                <TableCell>{i.data?.slice(0, 10)}</TableCell>
                <TableCell>{i.local}</TableCell>
                <TableCell>{i.riscoIdentificado}</TableCell>
                <TableCell>{i.grauRisco != null ? nivelRiscoLabel[i.grauRisco] : '—'}</TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    {i.naoConformidadeId ? (
                      <Badge appearance="tint" color="warning">
                        NC gerada
                      </Badge>
                    ) : (
                      <Button
                        appearance="subtle"
                        icon={<Warning24Regular />}
                        onClick={() => setGerandoNcPara(i.id)}
                        aria-label="Gerar não conformidade"
                      >
                        Gerar NC
                      </Button>
                    )}
                    <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(i.id)} aria-label="Excluir" />
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        )}
      </div>
    </div>
  );
}
