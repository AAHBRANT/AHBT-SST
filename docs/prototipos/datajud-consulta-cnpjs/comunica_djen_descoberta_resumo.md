# Descoberta de processos por nome da parte - API Comunica/DJEN (CNJ)

Data da sondagem: 2026-09-15

## Conclusao

A API publica DataJud **nao expoe partes/CNPJ** (o `_source` publico traz numero, classe, assunto,
orgao, movimentos; o `_mapping` retorna 403). Por isso a descoberta por nome/CNPJ no DataJud falha.

A API publica **Comunica / Diario de Justica Eletronico Nacional (DJEN)** do CNJ resolve a descoberta:

- Endpoint: `GET https://comunicaapi.pje.jus.br/api/v1/comunicacao`
- Sem autenticacao. Parametros validados: `nomeParte`, `siglaTribunal`, `numeroProcesso` (com ou sem
  mascara), `numeroOab` + `ufOab`, `texto`, `dataDisponibilizacaoInicio`, `dataDisponibilizacaoFim`,
  `pagina`, `itensPorPagina` (100 ok).
- `nomeParte` casa por nome do destinatario, sem sensibilidade a acento ("CUIA" e "CUIÁ" iguais).
- Filtro por CNPJ **nao existe** (`numeroCnpj`, `cnpj`, `numeroDocumento`, `documento` sao ignorados
  e retornam 10000 genericos). Alternativa: `texto=23.837.456/0001-06` busca o CNPJ no corpo da
  intimacao (achou 3 comunicacoes).
- Cada item traz: `numero_processo`, `numeroprocessocommascara`, `siglaTribunal`, `nomeOrgao`,
  `nomeClasse`, `tipoComunicacao`, `tipoDocumento`, `data_disponibilizacao`, `texto` (integra),
  `link` (documento no PJe), `destinatarios[{nome, polo}]`, `destinatarioadvogados[{advogado{nome,
  numero_oab, uf_oab}}]`.

## Resultados por nome monitorado (todos os tribunais)

| Nome | Comunicacoes | Processos distintos | Tribunais | Periodo |
|---|---|---|---|---|
| AAHBRANT (= AAHBRANT ENGENHARIA) | 384 | 88 | TRT13 41, TRT2 16, TRT19 12, TJSP 9, TRT4 3, TJRS 3, TRT7 2, TJPB 1, STJ 1 | 2023-08-22..2026-09-15 |
| CONSORCIO PARQUE ROGER FASE II | 50 | 4 | TRT13 2, TJPB 2 | 2025-03-27..2026-09-03 |
| CONSORCIO PONTE RIO CUIA | 2 | 1 | TJPB 1 | 2026-03-20..2026-09-14 |

Processos do Parque Roger no DJEN: `0000351-02.2025.5.13.0004` (TRT13), `0000567-57.2026.5.13.0026`
(TRT13), `0846247-52.2025.8.15.2001` (TJPB), `0803363-02.2025.8.15.2003` (TJPB).

Limite observado: `0800749-24.2025.8.15.0451` (arquivado 04/2026, achado no portal PJe/TJPB) **nao**
aparece no DJEN, porque nenhuma intimacao foi publicada tendo o consorcio como destinatario. Descoberta
por DJEN cobre processos com publicacao no DJEN desde ~2023; processos antigos/sem publicacao exigem
cadastro manual do numero CNJ.

## Portais dos tribunais (scraping)

- `consultapublica.tjpb.jus.br/pje/ConsultaPublica/listView.seam`: HTTP 429 com desafio Cloudflare
  ("Just a moment...") para cliente automatizado.
- `pje.trt13.jus.br/consultaprocessual/` e `pje-consulta-api`: HTTP 403 bloqueado pelo WAF CloudFront.
- Conclusao: conector por portal exige navegador headless com desafio anti-bot; fragil e nao
  recomendado como fonte automatica. Fica como importacao manual/assistida.

Script da sondagem: `sondar_comunica_djen.py`.
