# ESPECIFICAÇÃO TÉCNICA: CAMADA DE IDENTIFICAÇÃO FÍSICA & ÁREAS NFC
**Módulo:** G-SST / Identificação
**Versão:** 1.0.0
**Status:** Pronto para Implementação

---

## 1. OBJETIVO & ARQUITETURA DE CONCEITO

Implantar uma camada de identificação física no sistema G-SST baseada em infraestrutura NFC (NTAG215) e QR Code, conectando o mundo físico ao contexto SST da obra/unidade.

### Princípio Chave
> **A tecnologia de leitura (NFC/QR) NUNCA armazena dados de negócio.** 
> A Tag contém apenas seu identificador único. O sistema resolve o identificador e carrega a entidade correspondente no contexto correto de SST.

## 2. MODELO DE DADOS (DATABASE SCHEMA)

### Tabela: `identification_tags`
Armazena a infraestrutura de identificadores físicos.
```sql
CREATE TABLE identification_tags (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    uid VARCHAR(100) UNIQUE NOT NULL, -- UID do NFC (ex: 04:A2:XX...) ou Payload do QR
    type VARCHAR(20) NOT NULL CHECK (type IN ('NTAG215', 'NTAG213', 'QR_CODE', 'RFID')),
    status VARCHAR(20) NOT NULL DEFAULT 'AVAILABLE' CHECK (status IN ('AVAILABLE', 'BOUND', 'DISABLED', 'LOST')),
    bound_entity_type VARCHAR(50), -- 'AREA', 'ASSET', 'WORKER'
    bound_entity_id UUID,          -- ID da área ou ativo vinculado
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);


CREATE TABLE sst_areas (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) UNIQUE NOT NULL, -- Ex: AT-0017
    name VARCHAR(150) NOT NULL,
    type VARCHAR(50) NOT NULL,        -- Ex: 'WORK_AREA', 'RISK_ZONE', 'STORAGE'
    construction_site_id UUID NOT NULL, -- Vínculo com a Obra/Unidade
    location_details VARCHAR(255),    -- Ex: Torre Norte - Fachada Leste
    
    -- Riscos e Requisitos (Armazenados como JSONB para flexibilidade)
    risks JSONB NOT NULL DEFAULT '[]'::jsonb,      -- ['ALTURA', 'ELETRICO', 'ESPACO_CONFINADO']
    requirements JSONB NOT NULL DEFAULT '[]'::jsonb, -- ['NR35', 'APR', 'PT', 'EPI_OBRIGATORIO']
    
    status VARCHAR(20) DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE', 'BLOCKED')),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);