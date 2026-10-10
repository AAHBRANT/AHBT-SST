# Importação da estrutura de SST por obra (PGR + PCMSO)

Cada JSON aqui é a transcrição do PGR e do PCMSO oficiais de uma obra, no formato do endpoint
`POST /api/obras/{obraId}/estrutura-sst/importar` (GHE → funções → riscos, exames por função,
plano de ação e pendências de validação técnica).

| Arquivo | Obra | Fontes |
|---|---|---|
| `parque-roger_2026-10-09_v1.json` | Consórcio Parque Roger Fase II | PGR Rev. 01 (Eprotenge, 10/10/2024) e PCMSO EMMA (11/11/2024), pasta DATABOOK |

Para importar (a obra não pode ter GHE cadastrado):

```bash
curl -X POST "$API/api/obras/$OBRA_ID/estrutura-sst/importar" -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json; charset=utf-8" --data-binary @parque-roger_2026-10-09_v1.json
```

Os valores estão como no documento. As divergências encontradas na leitura entram como itens de
plano de ação com o prefixo `[Validação técnica]` e precisam ser validadas pelo engenheiro de
segurança (PGR) e pela médica coordenadora (PCMSO). A periodicidade do PCMSO foi lida pela ordem
da coluna, porque a coluna está desalinhada no PDF.
