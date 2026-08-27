# Ficha de EPI Reformulada — Design

**Data:** 2026-08-27
**Branch:** feature/matriz-epi-funcao
**Fase:** 2 de 3 do roadmap de reformulação do módulo EPI (Fase 1 — Matriz de EPI por Função — já implementada; Fase 3 — Estoque segmentado por Obra — spec separada, a seguir)

## Contexto

O módulo de EPI já tem: catálogo (`CatalogoEpi`), registro de entregas (`EntregaEpi`), Matriz de EPI por Função (Fase 1), assinatura eletrônica do entregador (técnico logado) e do receptor (trabalhador) via o motor genérico de assinatura, e geração de PDF via QuestPDF (`EntregaEpiPdfService`) já com a cor institucional `#670000`.

O documento de referência oficial (`AHBT-FIC-SSO-XXX-00_FichaEntregaEPI_2026-08-26_v01`) define um padrão diferente do implementado hoje: uma **ficha única por trabalhador**, cumulativa (uma tabela de entregas e uma de devoluções que crescem ao longo do tempo), com:

1. Identificação do trabalhador (nome, CPF, matrícula, função, turno, data de admissão, obra/frente de trabalho, empresa contratante, CNPJ da contratada)
2. Termo de Recebimento e Compromisso de Uso (5 cláusulas fixas, referência a treinamento NR-6, assinatura do empregado)
3. Controle de Entrega de EPI (tabela: Nº, EPI, CA, Motivo, Quantidade, Data, assinatura do empregado, assinatura do responsável pela entrega)
4. Controle de Devolução de EPI (tabela: Nº ref., EPI, Quantidade devolvida, Data, assinatura do empregado na devolução, visto do responsável)
5. Observação: retenção mínima de 20 anos após desligamento do trabalhador, para rastreabilidade

Hoje o sistema gera **um PDF por entrega individual**, não a ficha consolidada. Esta spec cobre a reformulação necessária para alinhar aos campos e à estrutura do modelo oficial.

## Decisões confirmadas com o usuário

- **Granularidade do documento:** ficha única por trabalhador (cumulativa), não mais um PDF por entrega.
- **Empresa contratante / CNPJ:** vem do cadastro de `Obra` (campo `Cliente` já existente + novo campo `Cnpj`). A `Obra` também recebe um campo de logo, usado no cabeçalho de todos os documentos gerados a partir de agora (com fallback em texto quando vazio).
- **Termo de Compromisso:** assinado a cada nova entrega, junto com a assinatura de recebimento do item — não é um termo único assinado uma vez. Na prática, a assinatura do receptor em cada entrega já representa a aceitação do termo; a UI só precisa exibir o texto do termo antes dessa assinatura.
- **Estoque segmentado por Obra/Almoxarifado:** fora desta spec — tratado na spec da Fase 3, já decidido que será segmentado só por Obra (sem conceito de Almoxarifado separado).
- **Migração de dados:** estritamente aditiva. Nenhuma coluna existente é removida ou tem seu valor reescrito automaticamente. Entregas antigas sem os novos campos aparecem como "não informado" na ficha.

## Fora de escopo (explicitamente adiado pelo usuário nesta sessão)

- Corrigir o bug de assinatura via WebAuthn/biometria (`"[object Object]" is not valid JSON`).
- Testar ponta a ponta o fluxo de assinatura por crachá/QR + PIN do receptor.
- Qualquer trabalho do bot de Telegram/DDS.
- Fase 3 (estoque segmentado) — spec própria.

## Modelo de dados

### `Obra` (`src/AAHBRANT.SST.Domain/Entidades/Obra.cs`)

Novos campos:

```csharp
public string? Cnpj { get; set; }
public byte[]? LogoConteudo { get; set; }
public string? LogoContentType { get; set; } // ex.: "image/png"
```

`Cliente` (já existente) passa a ser reaproveitado como "Empresa contratante" na ficha.

### `Trabalhador` (`src/AAHBRANT.SST.Domain/Entidades/Trabalhador.cs`)

Novo campo:

```csharp
public string? Turno { get; set; }
```

Texto livre — o modelo oficial não define uma lista fechada de turnos, então nenhuma lista fixa é assumida.

### `EntregaEpi` (`src/AAHBRANT.SST.Domain/Entidades/Epi.cs`)

Novo enum em `src/AAHBRANT.SST.Domain/Enums/Enums.cs`, espelhando exatamente as opções do modelo oficial:

```csharp
public enum MotivoEntregaEpi
{
    Inicial,
    Dano,
    Extravio,
    Vencimento,
    TrocaDeFuncao,
}
```

Novos campos em `EntregaEpi`:

```csharp
public MotivoEntregaEpi? MotivoTipo { get; set; }       // estruturado, obrigatório em entregas novas
public string? NumeroListaPresencaNr6 { get; set; }      // opcional
public DateTime? DataTreinamentoNr6 { get; set; }         // opcional
```

O campo `Motivo` (string livre) existente é **mantido sem alteração de nome ou tipo** — passa a ser tratado como observação complementar opcional, não mais o campo estruturado principal. `CriarEntregaEpiCommand`/`CriarEntregaEpiCommandValidator` passam a exigir `MotivoTipo` para novas entregas (validação de aplicação, não constraint de banco — a coluna é nullable para não quebrar dados já existentes).

### Devolução — sem mudança de schema no motor de assinatura

O motor de assinatura (`DocumentoAssinatura`/`DocumentoSignatario`) já é genérico via `(EntidadeTipo, EntidadeId)` e não precisa de nenhuma alteração de schema. A devolução passa a usar o literal `EntidadeTipo="DevolucaoEpi"` com `EntidadeId=EntregaEpi.Id` — o mesmo padrão já usado para `"Dds"` e `"EntregaEpi"`. Isso dá, de graça, dois signatários (trabalhador na devolução + responsável/consórcio que dá o visto), reaproveitando 100% do `CriarDocumentoAssinaturaCommand`, `RegistrarAssinaturaCommand`/`RegistrarAssinaturaSessaoLogadaCommand`, `ObterDocumentoQuery` e a UI de assinatura (`AssinaturaQuiosque`) já existentes.

## Fluxo de assinatura

**Entrega (ajuste no fluxo já existente):** o popup `AssinaturaEntregaEpiDialog.tsx` passa a exibir o texto das 5 cláusulas do Termo de Compromisso (texto estático, sem necessidade de nova entidade) antes do botão de assinatura do receptor. O formulário de nova entrega (`EntregasTab.tsx`) ganha o campo `Motivo` como dropdown (enum `MotivoEntregaEpi`) e os campos opcionais de NR-6.

**Devolução (fluxo novo):** ao registrar uma devolução, abre um novo componente `AssinaturaDevolucaoEpiDialog.tsx` — espelha a estrutura de `AssinaturaEntregaEpiDialog.tsx`, mas usa `entidadeTipo="DevolucaoEpi"` e rotula os signatários como "trabalhador (devolução)" e "responsável pela devolução" em vez de "entregador"/"receptor".

## Geração do PDF

`EntregaEpiPdfService` (`src/AAHBRANT.SST.Infrastructure/Documentos/EntregaEpiPdfService.cs`) e seu modelo `EntregaEpiPdfModelo` evoluem de "uma entrega" para "ficha consolidada de um trabalhador":

```csharp
public record FichaEpiPdfModelo(
    string ObraNome, string? ObraCnpj, byte[]? ObraLogoConteudo, string? ObraLogoContentType,
    string TrabalhadorNome, string TrabalhadorCpfMascarado, string TrabalhadorMatricula,
    string TrabalhadorFuncaoNome, string? TrabalhadorTurno, DateTime TrabalhadorDataAdmissao,
    List<LinhaEntregaEpiPdf> Entregas,
    List<LinhaDevolucaoEpiPdf> Devolucoes);

public record LinhaEntregaEpiPdf(
    int Numero, string EpiNome, string? CertificadoAprovacaoNumero, MotivoEntregaEpi? MotivoTipo,
    string? MotivoObservacao, int Quantidade, DateTime DataEntrega,
    bool AssinadoPeloEmpregado, bool AssinadoPeloResponsavel);

public record LinhaDevolucaoEpiPdf(
    int NumeroReferenciaEntrega, string EpiNome, int QuantidadeDevolvida, DateTime DataDevolucao,
    bool AssinadoPeloEmpregado, string? VistoResponsavel);
```

Nova query `ExportarFichaEpiTrabalhadorQuery(Guid TrabalhadorId)` substitui `ExportarEntregaEpiPdfQuery` (que ficava por `EntregaEpi.Id`). O endpoint `GET api/EntregasEpi/{id}/pdf` é substituído por `GET api/Trabalhadores/{trabalhadorId}/ficha-epi/pdf` (ou rota equivalente dentro do controller de EntregasEpi — decisão de organização de rota fica para o plano de implementação).

O layout segue as mesmas 4 seções do modelo oficial: identificação, termo de compromisso (texto fixo), tabela de entregas, tabela de devoluções — reaproveitando o cabeçalho com cor `#670000` já implementado, mais o logo da obra quando presente (fallback: nome da obra em texto).

## Frontend

- `src/AAHBRANT.SST.TeamsApp/src/pages/epi/EntregasTab.tsx`: campo Motivo vira `Dropdown` (Fluent UI) com as 5 opções do enum; botão de baixar PDF passa a apontar para a ficha do trabalhador (não mais por linha de entrega).
- `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaEntregaEpiDialog.tsx`: adiciona bloco de texto do Termo de Compromisso e campos NR-6 (opcionais).
- Novo `src/AAHBRANT.SST.TeamsApp/src/components/assinatura/AssinaturaDevolucaoEpiDialog.tsx`.
- Cadastro de Obra (localizar tela existente de Obras): novos campos CNPJ e upload de logo (`<input type="file">` + preview, seguindo padrão de upload já usado em Evidências, se houver um componente reaproveitável).

## Testes

- Backend: testes de handler para `CriarEntregaEpiCommand` (validação de `MotivoTipo` obrigatório), `ExportarFichaEpiTrabalhadorQuery` (agregação correta de múltiplas entregas/devoluções de um trabalhador), e o novo fluxo de assinatura de devolução via `EntidadeTipo="DevolucaoEpi"`.
- Frontend: verificação manual no navegador do fluxo completo (nova entrega com motivo estruturado → assinatura com termo exibido → devolução com assinatura → download da ficha consolidada em PDF), já que não há suíte de testes E2E automatizada identificada no projeto para este módulo.

## Riscos e observações

- **Legal/retenção:** o modelo oficial exige guarda mínima de 20 anos após desligamento do trabalhador. O sistema já usa soft-delete (`Ativo`) em todas as entidades via `AuditableEntity`, então não há exclusão física por padrão — mas isso deve ser confirmado/validado com a área jurídica ou de SST antes da emissão oficial do documento, como o próprio modelo observa ("numeração exata do(s) item(ns) vigente(s) a confirmar... antes da emissão oficial"). Recomendo não tratar este PDF como documento juridicamente definitivo até essa validação.
- **CPF:** a ficha exibe CPF do trabalhador — hoje já é armazenado criptografado (AES-256-GCM); o PDF deve seguir o padrão de mascaramento/exibição já usado em outros pontos do sistema que exibem CPF, não introduzir um novo formato.
