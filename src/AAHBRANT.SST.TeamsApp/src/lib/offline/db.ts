import Dexie, { type Table } from 'dexie';
import type { InspecaoDetalhe, ChecklistModeloDetalhe, Obra, Atividade, Usuario } from '../api';
import type { DadosFoto } from '../dadosFoto';

// Banco local (IndexedDB) do modo offline. Um por dispositivo/navegador — não é compartilhado
// entre usuários nem sincronizado por conta própria; é só a "caixa de saída" e o cache de leitura
// enquanto não há internet.

export interface ItemFilaSaida {
  id?: number;
  idempotencyKey: string;
  url: string;
  metodo: 'POST' | 'PUT' | 'DELETE';
  isFormData: boolean;
  corpoJson?: string;
  camposFormData?: Record<string, string>;
  arquivosFormData?: Record<string, Blob>;
  criadoEm: number;
  tentativas: number;
  ultimoErro?: string;
  // Conta (tenant:oid do token) que registrou a ação. Só ela reenvia: outra pessoa logada no mesmo
  // navegador não manda a ação em nome dela (auditoria 19/10, A04). Ausente em itens antigos.
  conta?: string;
  // Agrupa as dependências de uma inspeção: encerrar nunca ultrapassa respostas/fotos pendentes.
  inspecaoId?: string;
  versaoResposta?: string | null;
}

export interface RascunhoItemInspecao {
  versaoResposta?: string | null;
  descricao: string; statusItem: string; observacao: string; local: string;
  planoDeAcao: string; responsavelUsuarioId: string; prazo: string;
}

export interface InspecaoLocal {
  criadaLocalmente?: boolean;
  chave: string;
  conta: string;
  id: string;
  detalhe: InspecaoDetalhe;
  confirmado: InspecaoDetalhe;
  revisaoServidor: number;
  rascunhos: Record<string, RascunhoItemInspecao>;
  fotos: Record<string, { blob: Blob; dados?: DadosFoto; rascunho?: boolean }>;
  atualizadoEm: number;
}

export interface PreparacaoInspecoes {
  totalModelos?: number;
  conta: string;
  modelos: Record<string, ChecklistModeloDetalhe>;
  obras: Obra[];
  atividades: Atividade[];
  usuarios: Usuario[];
  atualizadoEm: number;
}

export interface ItemCacheJson {
  url: string;
  corpoJson: string;
  atualizadoEm: number;
}

export interface ItemCacheBlob {
  url: string;
  blob: Blob;
  atualizadoEm: number;
}

export interface ItemConflito {
  id?: number;
  url: string;
  metodo: string;
  mensagem: string;
  dadosDescartados?: string;
  dadosServidor?: string;
  criadoEm: number;
  lido: boolean;
  // Ação recusada pelo servidor, guardada inteira (inclusive a foto) para o botão "Tentar de novo" — antes
  // ficava só o texto "[multipart]" e a foto da assinatura se perdia. Campo sem índice: não muda o schema.
  item?: Omit<ItemFilaSaida, 'id'>;
}

class SstOfflineDb extends Dexie {
  filaSaida!: Table<ItemFilaSaida, number>;
  cacheJson!: Table<ItemCacheJson, string>;
  cacheBlob!: Table<ItemCacheBlob, string>;
  conflitos!: Table<ItemConflito, number>;
  inspecoesLocais!: Table<InspecaoLocal, string>;
  preparacaoInspecoes!: Table<PreparacaoInspecoes, string>;

  constructor() {
    super('aahbrant-sst-offline');
    this.version(1).stores({
      filaSaida: '++id, criadoEm',
      cacheJson: 'url, atualizadoEm',
      cacheBlob: 'url, atualizadoEm',
      conflitos: '++id, criadoEm',
    });
    this.version(2).stores({ inspecoesLocais: 'chave, conta, id' });
    this.version(3).stores({ preparacaoInspecoes: 'conta' });
  }
}

export const offlineDb = new SstOfflineDb();
