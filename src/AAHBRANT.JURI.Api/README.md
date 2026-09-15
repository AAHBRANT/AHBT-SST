# AAHBRANT.JURI.Api — busca de processos (MVP)

API interna do G-JURI que combina duas fontes públicas do CNJ:

| Fonte | Para quê | Autenticação |
|---|---|---|
| **DJEN / Comunica** (`comunicaapi.pje.jus.br`) | Descobrir processos **por nome da parte** e ler intimações/citações | Nenhuma |
| **DataJud** (`api-publica.datajud.cnj.jus.br`) | Detalhar processo **por número CNJ**: classe, assuntos, órgão, movimentos | Header `Authorization: APIKey …` (chave pública do CNJ, em `DataJud:ApiKey`) |

O DataJud não expõe partes/CNPJ na API pública, por isso a descoberta é feita pelo DJEN.
Sem banco, sem login e sem agendamento nesta versão.

## Rodar

```bash
dotnet run --project src/AAHBRANT.JURI.Api/AAHBRANT.JURI.Api.csproj --urls http://localhost:5210
```

Swagger na raiz: <http://localhost:5210/>

## Rotas

| Rota | Descrição |
|---|---|
| `GET /api/processos/buscar?nome=AAHBRANT` | Processos em que a parte aparece, agrupados por número CNJ. Padrão: só Justiça do Trabalho. |
| `GET /api/processos/monitorados` | O mesmo para as empresas de `PartesMonitoradas` (appsettings.json). |
| `GET /api/processos/{numeroCnj}` | Detalhe: movimentos (DataJud) + intimações (DJEN). Aceita com ou sem máscara. |
| `GET /health` | Verificação simples. |

Parâmetros opcionais de `buscar` e `monitorados`:

- `justica` = `trabalhista` (padrão) \| `estadual` \| `federal` \| `stj` \| `todas`
- `tribunal` = sigla DJEN (`TRT13`, `TJPB`…), só em `buscar`
- `desde` / `ate` = `AAAA-MM-DD` (data de disponibilização no DJEN)
- `incluirComunicacoes=true` = lista todas as intimações de cada processo
- `incluirTexto=true` = texto integral da intimação (padrão: 240 caracteres)

Parâmetros de `{numeroCnj}`: `maxMovimentos` (0 = todos), `incluirTexto`.

## Exemplos

```bash
curl "http://localhost:5210/api/processos/buscar?nome=CONSORCIO%20PARQUE%20ROGER%20FASE%20II"
curl "http://localhost:5210/api/processos/buscar?nome=AAHBRANT&tribunal=TRT2&desde=2026-01-01"
curl "http://localhost:5210/api/processos/0000567-57.2026.5.13.0026?maxMovimentos=10"
```

## Configuração (`appsettings.json`)

- `DataJud:ApiKey` — chave pública do CNJ. Pode ser rotacionada; troque aqui ou via variável de
  ambiente `DataJud__ApiKey`. Um 401 do DataJud aparece em `dataJudErro` na resposta.
- `PartesMonitoradas[]` — `Nome`, `Cnpj` e `Termos` (o que é enviado ao DJEN em `nomeParte`).
  O termo `AAHBRANT` cobre razão social antiga (EIRELI-EPP), atual (LTDA) e a filial PB.
- `Djen:MaxPaginas` — teto de páginas de 100 itens por termo (padrão 50).

## Limites conhecidos

- O DJEN só devolve processos que tiveram publicação no Diário Nacional (a partir de ~2023) tendo a
  parte como destinatária. Processo antigo ou sem intimação publicada (ex.:
  `0800749-24.2025.8.15.0451`) não é descoberto pelo nome, mas é detalhado normalmente pelo número.
- Não há filtro por CNPJ no DJEN; a busca é sempre por nome.
- Portais dos tribunais (PJe TJPB, TRT13) bloqueiam acesso automatizado e não são usados.
