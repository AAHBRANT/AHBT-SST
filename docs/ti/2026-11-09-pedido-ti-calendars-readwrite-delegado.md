# Pedido à TI: permissão delegada Calendars.ReadWrite no App Registration do SST

**Assunto:** Liberar permissão delegada "Calendars.ReadWrite" (Microsoft Graph) no app do SST

Olá, equipe de TI,

Solicitamos a inclusão de uma permissão no App Registration do sistema SST, para que as reuniões criadas pelo Calendário do sistema saiam em nome de quem as criou (como organizador), e não pela caixa sst@aahbrant.com.

**App Registration:** AAHBRANT SST (Application/Client ID `a7a22c22-5b01-4fd6-8c40-d4ddbd7d4904`)
**Tenant:** `8cc518ea-6df8-4d1e-a79f-89ee9314335c`

**O que fazer (Entra ID > Registros de aplicativo > AAHBRANT SST > Permissões de API):**
1. Adicionar uma permissão > Microsoft Graph > **Permissões delegadas** > **Calendars.ReadWrite**.
2. Clicar em **Conceder consentimento do administrador** para a AAHBRANT.

**Por que é seguro:**
- É uma permissão **delegada**: o sistema só age na agenda de quem está logado no app naquele momento, e com o login dessa pessoa. Não dá acesso às agendas de outras pessoas.
- **Não** é preciso mexer na Application Access Policy do Exchange (grupo SST-Envio-Graph). A caixa sst@ continua como está para e-mails e notificações.
- Enquanto a permissão não existir, o sistema continua criando as reuniões pela caixa sst@ (nada para de funcionar).

**Como validar depois:** criar uma reunião pelo Calendário do SST em hml. Ela deve aparecer na agenda de quem criou, com essa pessoa como organizadora.

Obrigado.
