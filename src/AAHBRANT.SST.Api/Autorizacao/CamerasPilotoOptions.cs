namespace AAHBRANT.SST.Api.Autorizacao;

// Seção "Cameras" do appsettings. UsuariosPiloto: e-mails (Administradores) com acesso ao módulo Câmeras
// durante o piloto. Lista vazia ou ausente = ninguém acessa (falha fechada).
public class CamerasPilotoOptions
{
    public List<string> UsuariosPiloto { get; set; } = [];
}
