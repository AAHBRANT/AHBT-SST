using AAHBRANT.SST.Application.Trabalhadores.Queries;

namespace AAHBRANT.SST.Application.Trabalhadores;

public interface IRelatorioFiscalizacaoPdfService
{
    byte[] Gerar(PerfilCompletoTrabalhadorDto perfil);
}
