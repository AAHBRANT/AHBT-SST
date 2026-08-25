import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
} from '@fluentui/react-components';
import { Add24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
import { api, type ItemHigienizacao, type NovoItemHigienizacao, type Obra } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function itemVazio(): NovoItemHigienizacao {
  return { obraId: '', nome: '', local: '', periodicidadeDias: 7 };
}

function vencimentoInfo(item: ItemHigienizacao): { texto: string; cor: 'success' | 'warning' | 'danger' } {
  const hoje = new Date();
  const vencimento = new Date(item.proximoVencimentoEm);
  const diffDias = Math.ceil((vencimento.getTime() - hoje.getTime()) / (1000 * 60 * 60 * 24));
  const texto = item.proximoVencimentoEm.slice(0, 10);
  if (diffDias < 0) return { texto: `${texto} (vencido)`, cor: 'danger' };
  if (diffDias <= 1) return { texto: `${texto} (amanhã)`, cor: 'warning' };
  return { texto, cor: 'success' };
}

// Controle de Higienização pedido pelo usuário em 24/08 (fora do MVP da §47, proposta própria):
// cadastro de locais com periodicidade de limpeza; o vencimento é calculado no backend a partir
// do último RegistroHigienizacao (ver ListarItensHigienizacaoQueryHandler.MapearParaDto).
export function HigienizacaoPage() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [itens, setItens] = useState<ItemHigienizacao[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novoItem, setNovoItem] = useState<NovoItemHigienizacao>(itemVazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaObras] = await Promise.all([api.higienizacao.listar(), api.obras.listar()]);
      setItens(lista);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar itens de higienização.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!novoItem.obraId || !novoItem.nome || novoItem.periodicidadeDias <= 0) {
      setErro('Preencha obra, nome do item e periodicidade (em dias).');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.higienizacao.criar(novoItem);
      setNovoItem(itemVazio());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao cadastrar item de higienização.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Controle de Higienização
        </Text>
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo item</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="Obra">
            <Select value={novoItem.obraId} onChange={(_, d) => setNovoItem({ ...novoItem, obraId: d.value })}>
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Nome (ex.: Banheiro, Refeitório)">
            <Input value={novoItem.nome} onChange={(_, d) => setNovoItem({ ...novoItem, nome: d.value })} />
          </Field>
          <Field label="Local (opcional)">
            <Input
              value={novoItem.local ?? ''}
              onChange={(_, d) => setNovoItem({ ...novoItem, local: d.value })}
            />
          </Field>
          <Field label="Periodicidade (dias)">
            <Input
              type="number"
              min={1}
              value={String(novoItem.periodicidadeDias)}
              onChange={(_, d) => setNovoItem({ ...novoItem, periodicidadeDias: Number(d.value) || 0 })}
            />
          </Field>
        </div>

        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Cadastrar item
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Itens cadastrados</Text>
        </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Item</TableHeaderCell>
              <TableHeaderCell>Local</TableHeaderCell>
              <TableHeaderCell>Periodicidade</TableHeaderCell>
              <TableHeaderCell>Última higienização</TableHeaderCell>
              <TableHeaderCell>Próximo vencimento</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {itens.map((item) => {
              const vencimento = vencimentoInfo(item);
              return (
                <TableRow
                  key={item.id}
                  onClick={() => navigate(`/prevencao/higienizacao/${item.id}`)}
                  style={{ cursor: 'pointer' }}
                >
                  <TableCell>{item.obraNome}</TableCell>
                  <TableCell>{item.nome}</TableCell>
                  <TableCell>{item.local}</TableCell>
                  <TableCell>A cada {item.periodicidadeDias} dias</TableCell>
                  <TableCell>{item.ultimaHigienizacaoEm?.slice(0, 10) ?? 'Nunca'}</TableCell>
                  <TableCell>
                    <Badge color={vencimento.cor} appearance="tint">
                      {vencimento.texto}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<ChevronRight24Regular />}
                      onClick={() => navigate(`/prevencao/higienizacao/${item.id}`)}
                      aria-label="Ver item"
                    />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
