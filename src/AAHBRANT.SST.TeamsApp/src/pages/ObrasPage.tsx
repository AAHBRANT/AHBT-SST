import { useEffect, useRef, useState } from 'react';
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
import { Add24Regular, ArrowUpload24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, statusObraLabel, StatusObra, type NovaObra, type Obra } from '../lib/api';
import { usePageStyles } from './pageStyles';

const obraVazia: NovaObra = {
  codigo: '',
  nome: '',
  cliente: '',
  status: StatusObra.Planejada,
  dataInicio: '',
  dataPrevisaoTermino: '',
  endereco: '',
  cidade: '',
  uf: '',
  cnpj: '',
};

export function ObrasPage() {
  const estilos = usePageStyles();
  const [obras, setObras] = useState<Obra[]>([]);
  const [novaObra, setNovaObra] = useState<NovaObra>(obraVazia);
  const [logoNovaObra, setLogoNovaObra] = useState<File | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [logoUrls, setLogoUrls] = useState<Record<string, string>>({});
  const [enviandoLogoId, setEnviandoLogoId] = useState<string | null>(null);
  const inputLogoRef = useRef<HTMLInputElement>(null);
  const obraAlvoLogoRef = useRef<string | null>(null);

  async function carregar() {
    try {
      setErro(null);
      setObras(await api.obras.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar obras.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Miniaturas do logo são baixadas sob demanda (só para obras com temLogo) e mantidas como
  // object URL até a página ser desmontada — diferente do padrão "baixar PDF" já usado no
  // restante do app (que cria e revoga a URL na mesma função), pois aqui a URL precisa
  // permanecer viva para o <img> renderizar.
  useEffect(() => {
    let cancelado = false;
    (async () => {
      for (const obra of obras) {
        if (!obra.temLogo || logoUrls[obra.id]) continue;
        try {
          const blob = await api.obras.baixarLogo(obra.id);
          if (cancelado) return;
          setLogoUrls((atual) => ({ ...atual, [obra.id]: URL.createObjectURL(blob) }));
        } catch {
          // Falha ao carregar miniatura não impede o uso da página; a obra fica sem preview.
        }
      }
    })();
    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obras]);

  useEffect(() => {
    return () => {
      Object.values(logoUrls).forEach((url) => URL.revokeObjectURL(url));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function abrirSeletorLogo(obraId: string) {
    obraAlvoLogoRef.current = obraId;
    inputLogoRef.current?.click();
  }

  async function enviarLogo(arquivo: File | null) {
    const obraId = obraAlvoLogoRef.current;
    if (!arquivo || !obraId) return;
    try {
      setEnviandoLogoId(obraId);
      setErro(null);
      await api.obras.anexarLogo(obraId, arquivo);
      setLogoUrls((atual) => {
        const anterior = atual[obraId];
        if (anterior) URL.revokeObjectURL(anterior);
        const { [obraId]: _removido, ...resto } = atual;
        return resto;
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar o logo.');
    } finally {
      setEnviandoLogoId(null);
    }
  }

  async function criar() {
    if (!logoNovaObra) {
      setErro('A logomarca da obra é obrigatória para finalizar o cadastro.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.obras.criar(
        {
          ...novaObra,
          dataInicio: novaObra.dataInicio || null,
          dataPrevisaoTermino: novaObra.dataPrevisaoTermino || null,
        },
        logoNovaObra,
      );
      setNovaObra(obraVazia);
      setLogoNovaObra(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar obra.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.obras.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir obra.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Obras cadastradas</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Código">
          <Input value={novaObra.codigo} onChange={(_, d) => setNovaObra({ ...novaObra, codigo: d.value })} />
        </Field>
        <Field label="Nome">
          <Input value={novaObra.nome} onChange={(_, d) => setNovaObra({ ...novaObra, nome: d.value })} />
        </Field>
        <Field label="Cliente">
          <Input
            value={novaObra.cliente ?? ''}
            onChange={(_, d) => setNovaObra({ ...novaObra, cliente: d.value })}
          />
        </Field>
        <Field label="Status">
          <Select
            value={novaObra.status}
            onChange={(_, d) => setNovaObra({ ...novaObra, status: Number(d.value) })}
          >
            {Object.entries(statusObraLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Data de início">
          <Input
            type="date"
            value={novaObra.dataInicio ?? ''}
            onChange={(_, d) => setNovaObra({ ...novaObra, dataInicio: d.value })}
          />
        </Field>
        <Field label="Previsão de término">
          <Input
            type="date"
            value={novaObra.dataPrevisaoTermino ?? ''}
            onChange={(_, d) => setNovaObra({ ...novaObra, dataPrevisaoTermino: d.value })}
          />
        </Field>
        <Field label="Endereço">
          <Input
            value={novaObra.endereco ?? ''}
            onChange={(_, d) => setNovaObra({ ...novaObra, endereco: d.value })}
          />
        </Field>
        <Field label="Cidade">
          <Input value={novaObra.cidade ?? ''} onChange={(_, d) => setNovaObra({ ...novaObra, cidade: d.value })} />
        </Field>
        <Field label="UF">
          <Input
            value={novaObra.uf ?? ''}
            maxLength={2}
            onChange={(_, d) => setNovaObra({ ...novaObra, uf: d.value.toUpperCase() })}
          />
        </Field>
        <Field label="CNPJ">
          <Input
            value={novaObra.cnpj ?? ''}
            maxLength={18}
            onChange={(_, d) => setNovaObra({ ...novaObra, cnpj: d.value })}
          />
        </Field>
        <Field label="Logomarca da obra" required>
          <input
            type="file"
            accept="image/jpeg,image/png"
            onChange={(e) => setLogoNovaObra(e.target.files?.[0] ?? null)}
          />
        </Field>
      </div>
      <Text size={200}>
        A logomarca é obrigatória: ela será usada no cabeçalho dos documentos gerados e assinados
        para esta obra (APR, PT, DDS, Ficha de EPI, Relatório de Fiscalização).
      </Text>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !logoNovaObra}>
          Adicionar obra
        </Button>
      </div>

      <input
        ref={inputLogoRef}
        type="file"
        accept="image/*"
        style={{ display: 'none' }}
        onChange={(e) => {
          enviarLogo(e.target.files?.[0] ?? null);
          e.target.value = '';
        }}
      />

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Código</TableHeaderCell>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Cliente</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell>Cidade/UF</TableHeaderCell>
            <TableHeaderCell>CNPJ</TableHeaderCell>
            <TableHeaderCell>Logo</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {obras.map((obra) => (
            <TableRow key={obra.id}>
              <TableCell>{obra.codigo}</TableCell>
              <TableCell>{obra.nome}</TableCell>
              <TableCell>{obra.cliente}</TableCell>
              <TableCell>{statusObraLabel[obra.status]}</TableCell>
              <TableCell>
                {obra.cidade}
                {obra.uf ? `/${obra.uf}` : ''}
              </TableCell>
              <TableCell>{obra.cnpj}</TableCell>
              <TableCell>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  {logoUrls[obra.id] && (
                    <img
                      src={logoUrls[obra.id]}
                      alt={`Logo de ${obra.nome}`}
                      style={{ height: 32, width: 32, objectFit: 'contain', borderRadius: 4 }}
                    />
                  )}
                  <Button
                    appearance="subtle"
                    icon={<ArrowUpload24Regular />}
                    onClick={() => abrirSeletorLogo(obra.id)}
                    disabled={enviandoLogoId === obra.id}
                    aria-label="Enviar logo"
                  />
                </div>
              </TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(obra.id)}
                  aria-label="Excluir"
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
