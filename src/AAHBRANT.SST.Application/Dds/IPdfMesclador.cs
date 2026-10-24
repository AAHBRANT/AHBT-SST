namespace AAHBRANT.SST.Application.Dds;

// Junta vários PDFs em um só, na ordem recebida (usado por "Baixar semana": DDS semanal + os
// diários). Mora na Application como interface porque só a Infrastructure conhece o QuestPDF.
public interface IPdfMesclador
{
    byte[] Mesclar(IReadOnlyList<byte[]> pdfs);
}
