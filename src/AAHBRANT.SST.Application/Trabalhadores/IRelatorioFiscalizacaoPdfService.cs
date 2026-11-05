using AAHBRANT.SST.Application.Trabalhadores.Queries;

namespace AAHBRANT.SST.Application.Trabalhadores;

// Dados de identificação que só o PDF usa e que não podem ir no DTO do perfil (que também sai pela API
// em JSON): a foto de cadastro em bytes e os dados da obra para "Empresa contratante" e CNPJ.
// ReferenciasAssinatura (05/11): por signatário, a referência biométrica vigente na data da assinatura —
// foto facial cadastrada (assinatura facial) ou imagem da digital cadastrada (assinatura por digital) —
// para o cofre mostrar cadastro × assinatura lado a lado, como na Ficha de EPI.
public record IdentificacaoRelatorioFiscalizacao(byte[]? Foto, string? ObraCliente, string? ObraCnpj,
    IReadOnlyDictionary<Guid, byte[]>? ReferenciasAssinatura = null);

public interface IRelatorioFiscalizacaoPdfService
{
    byte[] Gerar(PerfilCompletoTrabalhadorDto perfil, IdentificacaoRelatorioFiscalizacao? identificacao = null);
}
