using AAHBRANT.SST.Application.Trabalhadores.Queries;

namespace AAHBRANT.SST.Application.Trabalhadores;

// Dados de identificação que só o PDF usa e que não podem ir no DTO do perfil (que também sai pela API
// em JSON): a foto de cadastro em bytes e os dados da obra para "Empresa contratante" e CNPJ.
public record IdentificacaoRelatorioFiscalizacao(byte[]? Foto, string? ObraCliente, string? ObraCnpj);

public interface IRelatorioFiscalizacaoPdfService
{
    byte[] Gerar(PerfilCompletoTrabalhadorDto perfil, IdentificacaoRelatorioFiscalizacao? identificacao = null);
}
