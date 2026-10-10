import { useState } from 'react';
import {
  Button,
  CampoData,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Field,
  Input,
  Text,
} from '@ui';
import { SeletorFotoCamera } from '../SeletorFotoCamera';
import type { DadosNovaRevisao } from '../../lib/api';

// "Nova revisão" do PGR e do PCMSO (aprovado em 10/10/2026), no lugar de "Substituir documento": o PDF
// novo vira uma revisão e o anterior fica guardado no histórico. Número em branco = o seguinte ao último.
export function DialogoNovaRevisao({
  aberto,
  documento,
  aoFechar,
  salvar,
  aoSalvar,
  acaoExtra,
}: {
  aberto: boolean;
  documento: 'PGR' | 'PCMSO';
  aoFechar: () => void;
  salvar: (dados: DadosNovaRevisao) => Promise<{ id: string }>;
  aoSalvar: (revisaoId: string) => void;
  // Segundo botão de salvar (ex.: "Salvar e ler com IA"); recebe o id da revisão criada.
  acaoExtra?: { rotulo: string; aoSalvar: (revisaoId: string) => void };
}) {
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [numero, setNumero] = useState('');
  const [data, setData] = useState('');
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  function limpar() {
    setArquivo(null);
    setNumero('');
    setData('');
    setMotivo('');
    setErro(null);
  }

  async function enviar(depois: (id: string) => void) {
    if (!arquivo || !data || !motivo.trim()) {
      setErro('Anexe o PDF e informe a data e o motivo da revisão.');
      return;
    }
    const numeroRevisao = numero.trim() === '' ? null : Number(numero);
    if (numeroRevisao != null && (!Number.isInteger(numeroRevisao) || numeroRevisao < 0)) {
      setErro('O número da revisão deve ser um inteiro a partir de 0.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      const { id } = await salvar({ numeroRevisao, dataRevisao: data, motivo: motivo.trim(), arquivo });
      limpar();
      depois(id);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar a revisão.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Dialog
      open={aberto}
      onOpenChange={(_, d) => {
        if (!d.open && !salvando) {
          limpar();
          aoFechar();
        }
      }}
    >
      <DialogSurface style={{ maxWidth: 560 }}>
        <DialogBody>
          <DialogTitle>Nova revisão do {documento}</DialogTitle>
          <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {erro && (
              <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
                {erro}
              </FeedbackInline>
            )}
            <Text size={200}>O PDF atual não é apagado: ele fica guardado no histórico de revisões.</Text>
            <Field label="PDF da revisão" required>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                <SeletorFotoCamera
                  rotulo={arquivo ? 'Trocar arquivo' : 'Escolher PDF'}
                  tiposAceitos="application/pdf"
                  tamanhoMaximoMb={20}
                  permitirCamera={false}
                  aoSelecionarArquivo={setArquivo}
                  aoErroValidacao={setErro}
                />
                {arquivo && <Text weight="semibold">{arquivo.name}</Text>}
              </div>
            </Field>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12 }}>
              <Field label="Revisão nº" hint="Em branco: a seguinte à última">
                <Input type="number" min={0} value={numero} onChange={(_, d) => setNumero(d.value)} />
              </Field>
              <Field label="Data da revisão" required>
                <CampoData value={data} onChange={(_, d) => setData(d.value)} />
              </Field>
            </div>
            <Field label="Motivo" required>
              <Input value={motivo} onChange={(_, d) => setMotivo(d.value)} maxLength={500} />
            </Field>
          </DialogContent>
          <DialogActions>
            <Button
              appearance="secondary"
              disabled={salvando}
              onClick={() => {
                limpar();
                aoFechar();
              }}
            >
              Cancelar
            </Button>
            <Button appearance={acaoExtra ? 'secondary' : 'primary'} disabled={salvando} onClick={() => enviar(aoSalvar)}>
              Salvar revisão
            </Button>
            {acaoExtra && (
              <Button appearance="primary" disabled={salvando} onClick={() => enviar(acaoExtra.aoSalvar)}>
                {acaoExtra.rotulo}
              </Button>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
