import Dexie, { type Table } from 'dexie';

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
}

class SstOfflineDb extends Dexie {
  filaSaida!: Table<ItemFilaSaida, number>;
  cacheJson!: Table<ItemCacheJson, string>;
  cacheBlob!: Table<ItemCacheBlob, string>;
  conflitos!: Table<ItemConflito, number>;

  constructor() {
    super('aahbrant-sst-offline');
    this.version(1).stores({
      filaSaida: '++id, criadoEm',
      cacheJson: 'url, atualizadoEm',
      cacheBlob: 'url, atualizadoEm',
      conflitos: '++id, criadoEm',
    });
  }
}

export const offlineDb = new SstOfflineDb();
