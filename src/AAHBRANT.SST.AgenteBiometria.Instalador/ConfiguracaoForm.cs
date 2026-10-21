namespace AAHBRANT.SST.AgenteBiometria.Instalador;

// Tela da primeira instalação: os dados do posto saem do cadastro do leitor no SST (Obras > Leitores de digital).
public class ConfiguracaoForm : Form
{
    private readonly TextBox _dispositivoId = new();
    private readonly TextBox _segredo = new() { UseSystemPasswordChar = true };
    private readonly TextBox _chave = new() { UseSystemPasswordChar = true };
    private readonly TextBox _backend = new();
    private readonly TextBox _origem = new();
    private readonly TextBox _manifesto = new();

    public ConfiguracaoPosto? Resultado { get; private set; }

    public ConfiguracaoForm(string backendPadrao, string origemPadrao, string manifestoPadrao)
    {
        Text = "Instalar Agente de Biometria AAHBRANT";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        _backend.Text = backendPadrao;
        _origem.Text = origemPadrao;
        _manifesto.Text = manifestoPadrao;

        var tabela = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        void Linha(string rotulo, Control campo)
        {
            campo.Width = 380;
            tabela.Controls.Add(new Label { Text = rotulo, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 12, 0) });
            tabela.Controls.Add(campo);
        }
        Linha("Id do dispositivo", _dispositivoId);
        Linha("Segredo do dispositivo", _segredo);
        Linha("Chave de criptografia (base64)", _chave);
        Linha("URL da API", _backend);
        Linha("URL do app (quiosque)", _origem);
        Linha("URL de atualização", _manifesto);

        var instalar = new Button { Text = "Instalar", AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
        instalar.Click += (_, _) => Confirmar();
        AcceptButton = instalar;
        tabela.Controls.Add(new Label());
        tabela.Controls.Add(instalar);
        Controls.Add(tabela);
    }

    private void Confirmar()
    {
        if (!Guid.TryParse(_dispositivoId.Text.Trim(), out var id) || id == Guid.Empty)
        {
            Avisar("Id do dispositivo inválido.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_segredo.Text) || string.IsNullOrWhiteSpace(_chave.Text))
        {
            Avisar("Preencha o segredo do dispositivo e a chave de criptografia.");
            return;
        }
        if (!EhHttps(_backend.Text) || !EhHttps(_origem.Text) || (_manifesto.Text.Trim().Length > 0 && !EhHttps(_manifesto.Text)))
        {
            Avisar("As URLs precisam começar com https://.");
            return;
        }

        Resultado = new ConfiguracaoPosto(id, _segredo.Text.Trim(), _chave.Text.Trim(),
            _backend.Text.Trim().TrimEnd('/'), _origem.Text.Trim().TrimEnd('/'), _manifesto.Text.Trim());
        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool EhHttps(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;

    private void Avisar(string mensagem) =>
        MessageBox.Show(this, mensagem, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
