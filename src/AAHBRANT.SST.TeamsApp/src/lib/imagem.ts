const LADO_MAXIMO_PX = 1600;
const QUALIDADE_JPEG = 0.8;

// Fotos de câmera de celular vêm em resolução plena (3-8 MB) mas são exibidas em miniaturas
// pequenas nas telas do sistema — sem isso, cada upload gasta banda/tempo à toa e engorda o banco
// (todo arquivo é gravado como byte[] direto numa tabela, não há storage próprio). Redesenha a
// imagem num <canvas> reduzindo pro lado maior de 1600px e reexporta como JPEG 80% de qualidade
// antes de checar o limite de tamanho. Se falhar (formato exótico, navegador sem suporte), segue
// com o arquivo original — a checagem de tamanho no chamador continua valendo como rede de segurança.
//
// Extraído de SeletorFotoCamera.tsx (04/09) para ser reaproveitado também por GradeFotosEvidencia.tsx.
export async function comprimirImagem(arquivo: File): Promise<File> {
  const bitmap = await createImageBitmap(arquivo);
  try {
    const escala = Math.min(1, LADO_MAXIMO_PX / Math.max(bitmap.width, bitmap.height));
    const largura = Math.round(bitmap.width * escala);
    const altura = Math.round(bitmap.height * escala);

    const canvas = document.createElement('canvas');
    canvas.width = largura;
    canvas.height = altura;
    const contexto = canvas.getContext('2d');
    if (!contexto) return arquivo;
    contexto.drawImage(bitmap, 0, 0, largura, altura);

    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', QUALIDADE_JPEG));
    if (!blob) return arquivo;

    const novoNome = arquivo.name.replace(/\.[^./]+$/, '') + '.jpg';
    return new File([blob], novoNome, { type: 'image/jpeg' });
  } finally {
    bitmap.close();
  }
}
