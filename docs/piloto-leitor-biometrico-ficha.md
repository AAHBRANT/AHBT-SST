# Ficha de registro do piloto — leitor de digital

Preencha durante o piloto (imprimir ou copiar para um documento). Roteiro completo em
`docs/piloto-leitor-biometrico.md`. Ao final, envie esta ficha **e** a saída do `diagnostico.ps1` para quem
acompanha o piloto.

## 1. Ambiente

| Item | Preencher |
|---|---|
| Data / responsável | |
| Obra e nome do posto registrado (Administração › Leitores de digital) | |
| PC (nome, Windows) | |
| Leitor (modelo/etiqueta, nº de série se houver) | |
| Navegador e versão (Chrome/Edge) | |
| Teams (desktop/web e versão) | |
| URL do app em hml | |
| Versão do agente / data do pacote | |
| Deploy de hml conferido (checklist do resumo de deploy) | ☐ sim ☐ não |

## 2. Antes dos testes

| Passo | OK? | Observação |
|---|---|---|
| Driver Futronic instalado; leitor sem alerta no Gerenciador de Dispositivos | ☐ | |
| `instalar.ps1` executado com os dados do registro | ☐ | |
| `diagnostico.ps1` sem falhas (cole a saída abaixo ou anexe) | ☐ | |
| Leitor aparece como **Conectado** em Administração › Leitores de digital | ☐ | |
| Termo de consentimento aprovado pelo jurídico **ou** teste feito só com voluntários informados | ☐ | |

Saída do diagnóstico (colar):

```
```

Se o diagnóstico falhar, anote a linha `[FALHOU]` e a dica abaixo dela; quase todo problema conhecido aparece ali.

## 3. Testes

Marque **OK**, **FALHOU** ou **N/A** e escreva a mensagem exata da tela em caso de falha.

| # | Teste | Chrome/Edge | Teams | Observação / mensagem |
|---|---|---|---|---|
| 1 | Cadastrar a digital (2 leituras do mesmo dedo) | | | |
| 1b | Cadastro com dedos diferentes → recusado, nada gravado | | | |
| 1c | Cadastro sem tirar o dedo → aviso "O dedo não foi retirado…" | | | |
| 2 | (repetir o 1 dentro do Teams) | — | | |
| 3 | Assinar um documento pelo quiosque com o dedo cadastrado (bipe toca?) | | | |
| 4 | Assinar com outro dedo/pessoa → recusado | | | |
| 5 | Sem apoiar o dedo por 15 s → mensagem "Nenhum dedo detectado…" | | | |
| 6 | Cadastrar a facial (guia + OK) e assinar por facial | | | |
| 7 | DDS: presença por digital e por facial em funcionários diferentes | | | |

## 4. Pessoas e dedos (mínimo 5 pessoas, dedos variados)

Use iniciais ou um código, não o nome completo. "Falso aceite" = uma pessoa/dedo **não cadastrado** foi aceito
como se fosse outra — **qualquer ocorrência é problema grave**: pare o piloto e avise.

| # | Pessoa (código) | Dedo | Cadastro aceito? | Assinou com o próprio dedo? (tentativas até dar certo) | Outro dedo foi recusado? | Falso aceite? |
|---|---|---|---|---|---|---|
| 1 | | | ☐ | | ☐ | ☐ não ☐ **sim** |
| 2 | | | ☐ | | ☐ | ☐ não ☐ **sim** |
| 3 | | | ☐ | | ☐ | ☐ não ☐ **sim** |
| 4 | | | ☐ | | ☐ | ☐ não ☐ **sim** |
| 5 | | | ☐ | | ☐ | ☐ não ☐ **sim** |

Dedos que costumam dar problema (anote se aparecerem): seco, com calo/corte, suado, sujo (cimento, tinta),
com creme, muito claro/desgastado.

## 5. Critério para seguir

- ☐ Testes 1 a 7 (incluindo 1b e 1c) sem falha, no navegador **e** no Teams.
- ☐ Nenhum falso aceite nas 5 pessoas.
- ☐ Cada pessoa conseguiu assinar com o próprio dedo em, no máximo, 2 tentativas.
- ☐ Termo de consentimento revisado pelo jurídico (rascunho em `docs/juridico/consentimento-biometria-rascunho.md`).

**Decisão:** ☐ seguir para as demais obras ☐ repetir o piloto ☐ interromper. Responsável: __________ Data: ______
