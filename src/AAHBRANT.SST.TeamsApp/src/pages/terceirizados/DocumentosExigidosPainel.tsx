import { useEffect, useState } from 'react';
import { Add24Regular, Edit24Regular } from '@fluentui/react-icons';
import {
  BotaoAcao,
  Button,
  Checkbox,
  FeedbackInline,
  Field,
  Input,
  PainelLateral,
  StatusChip,
  Text,
  Textarea,
  designTokens,
  tokensUi,
} from '@ui';
import { api, type DadosDocumentoExigidoEmpresa, type DocumentoExigidoEmpresa } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Lista de documentos que a AAHBRANT exige de toda empresa terceirizada (checklist da empresa,
// 21/10/2026). Nasce com PGR, PCMSO, cartão CNPJ e contrato social; precisa ser validada com o
// Jurídico. Desativar tira o documento do checklist de todas as empresas sem apagar o que já foi enviado.

const formularioVazio: DadosDocumentoExigidoEmpresa = { nome: '', descricao: '', exigeValidade: true, ativo: true };

export function DocumentosExigidosPainel({
  aberto,
  aoFechar,
  aoAlterar,
}: {
  aberto: boolean;
  aoFechar: () => void;
  aoAlterar: () => void;
}) {
  const sucessoToast = useSucessoToast();
  const [documentos, setDocumentos] = useState<DocumentoExigidoEmpresa[] | null>(null);
  const [edicao, setEdicao] = useState<{ id: string | null; dados: DadosDocumentoExigidoEmpresa } | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    if (!aberto) return;
    let ativo = true;
    api.terceirizados.documentosExigidos
      .listar()
      .then((lista) => {
        if (ativo) setDocumentos(lista);
      })
      .catch((e: unknown) => {
        if (ativo) setErro(e instanceof Error ? e.message : 'Falha ao carregar os documentos exigidos.');
      });
    return () => {
      ativo = false;
    };
  }, [aberto]);

  async function salvar() {
    if (!edicao) return;
    if (!edicao.dados.nome.trim()) {
      setErro('Informe o nome do documento.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      if (edicao.id) await api.terceirizados.documentosExigidos.atualizar(edicao.id, edicao.dados);
      else await api.terceirizados.documentosExigidos.criar(edicao.dados);
      setDocumentos(await api.terceirizados.documentosExigidos.listar());
      sucessoToast(edicao.id ? 'Documento exigido atualizado.' : 'Documento exigido incluído.');
      setEdicao(null);
      aoAlterar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar o documento exigido.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <PainelLateral
      aberto={aberto}
      aoFechar={() => {
        setEdicao(null);
        setErro(null);
        aoFechar();
      }}
      titulo="Documentos exigidos das empresas"
      subtitulo="Valem para o checklist de todas as empresas terceirizadas."
      rodape={
        edicao ? (
          <>
            <Button onClick={() => setEdicao(null)}>Cancelar</Button>
            <Button appearance="primary" disabled={salvando} onClick={() => void salvar()}>
              {salvando ? 'Salvando…' : 'Salvar'}
            </Button>
          </>
        ) : (
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setEdicao({ id: null, dados: formularioVazio })}>
            Incluir documento
          </Button>
        )
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md }}>
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}
        <FeedbackInline tom="info">
          Lista inicial sugerida (PGR, PCMSO, cartão CNPJ e contrato social). Valide com o Jurídico antes de cobrar das empresas.
        </FeedbackInline>

        {edicao && (
          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              gap: tokensUi.espaco.md,
              padding: tokensUi.espaco.lg,
              border: `1px solid ${tokensUi.bordaSuave}`,
              borderRadius: tokensUi.raio.md,
            }}
          >
            <Field label="Nome" required>
              <Input
                value={edicao.dados.nome}
                maxLength={120}
                onChange={(_, d) => setEdicao({ ...edicao, dados: { ...edicao.dados, nome: d.value } })}
              />
            </Field>
            <Field label="Descrição">
              <Textarea
                value={edicao.dados.descricao ?? ''}
                maxLength={500}
                onChange={(_, d) => setEdicao({ ...edicao, dados: { ...edicao.dados, descricao: d.value } })}
              />
            </Field>
            <Checkbox
              label="Tem data de validade (vence e gera alerta)"
              checked={edicao.dados.exigeValidade}
              onChange={(_, d) => setEdicao({ ...edicao, dados: { ...edicao.dados, exigeValidade: !!d.checked } })}
            />
            <Checkbox
              label="Exigir no checklist das empresas"
              checked={edicao.dados.ativo}
              onChange={(_, d) => setEdicao({ ...edicao, dados: { ...edicao.dados, ativo: !!d.checked } })}
            />
          </div>
        )}

        {documentos?.map((d) => (
          <div
            key={d.id}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: tokensUi.espaco.sm,
              padding: `${tokensUi.espaco.sm} ${tokensUi.espaco.md}`,
              border: `1px solid ${tokensUi.bordaSuave}`,
              borderRadius: tokensUi.raio.md,
              opacity: d.ativo ? 1 : 0.6,
            }}
          >
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontWeight: 600 }}>{d.nome}</div>
              {d.descricao && <div style={{ fontSize: 12.5, color: designTokens.colorNeutralMedium }}>{d.descricao}</div>}
              <div style={{ display: 'flex', gap: 6, marginTop: 4, flexWrap: 'wrap' }}>
                <StatusChip tom="neutro">{d.exigeValidade ? 'Com validade' : 'Sem validade'}</StatusChip>
                {!d.ativo && <StatusChip tom="atencao">Não exigido</StatusChip>}
              </div>
            </div>
            <BotaoAcao
              tom="ver"
              icon={<Edit24Regular />}
              onClick={() =>
                setEdicao({ id: d.id, dados: { nome: d.nome, descricao: d.descricao ?? '', exigeValidade: d.exigeValidade, ativo: d.ativo } })
              }
              aria-label={`Editar ${d.nome}`}
            />
          </div>
        ))}
        {documentos?.length === 0 && <Text size={200}>Nenhum documento exigido. Use "Incluir documento".</Text>}
      </div>
    </PainelLateral>
  );
}
