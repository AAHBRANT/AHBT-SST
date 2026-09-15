# Pedido de integração: Alojamentos (G-RH → SST-APP)

Oi! Estamos criando uma aba de inspeção de Alojamento no SST-APP e precisamos puxar os dados de
Alojamento que já existem no G-RH (vi a tela — "ALOJAMENTO - 02", "ALOJAMENTO - COLABORADORES" etc.,
com moradores vinculados por colaborador).

Queremos seguir **exatamente o mesmo padrão** que já usamos para Colaborador (contrato acordado em
09/09/2026): um endpoint de carga inicial + uma fila de Service Bus para eventos contínuos, os dois
com o mesmo formato de payload.

## O que precisamos do lado do G-RH

**1. Endpoint de carga inicial** (mesmo padrão de `GET /api/integracoes/sst/colaboradores`):
```
GET /api/integracoes/sst/alojamentos
```
Autenticação: mesmo App Registration/client-credentials que já usamos para Colaboradores — só
precisamos que esse novo recurso seja liberado pro nosso App Role (como foi feito em 14/09 para
Colaboradores, que teve um 403 até liberarem o Role certo).

**2. Fila de Service Bus para eventos contínuos** (mesmo padrão da fila `colaborador-grh`):
```
alojamento-grh
```
Publicada sempre que um alojamento for criado/editado, ou um morador for adicionado/removido —
mesmo payload do endpoint de carga inicial, reaproveitando a serialização (é o que já fazem hoje
pra Colaborador).

## Formato do payload proposto (ajustem se fizer sentido do lado de vocês)

```json
{
  "id": "identificador único do alojamento no G-RH (guid ou int, o que vocês já usarem)",
  "nome": "ALOJAMENTO - 02",
  "obraNome": "Consórcio Ponte Rio Cuiá",
  "endereco": "Rua Recife, 164, Planalto Boa Esperança",
  "custoMensal": null,
  "moradores": [
    { "cpf": "00000000000", "matricula": "5", "desde": "2026-09-15" }
  ]
}
```

Motivo de vincular morador só por `cpf`/`matricula` (sem repetir nome, obra etc. do colaborador):
esses dados já vêm pela integração de Colaborador que já existe — não precisamos duplicar, só
precisamos saber "quem mora em qual alojamento, desde quando".

## Por que precisamos disso

Vamos criar uma aba "Alojamento" dentro de Inspeções no SST-APP: cards por obra (só as que o
usuário tem acesso), e dentro de cada obra, um card por alojamento cadastrado. Ao clicar, abre
direto a inspeção do checklist de alojamento (30 itens, já existe no SST-APP) pré-preenchida,
pra facilitar a vida do técnico que faz a inspeção em campo.

Se tiverem dúvida em algum campo ou quiserem propor um formato diferente, é só responder — a gente
adapta o lado do SST-APP pro que for combinado.

Obrigado!
