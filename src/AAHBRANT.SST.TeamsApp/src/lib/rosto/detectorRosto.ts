import type { FaceDetector } from '@mediapipe/tasks-vision';
import type { CaixaRosto } from './avaliarRosto';

// Detector de rosto que roda DENTRO do aparelho (MediaPipe BlazeFace): nenhuma imagem sai por causa
// dele. Carregado só quando a câmera facial abre pela primeira vez; o arquivo .wasm tem ~13 MB (cerca
// de 3,8 MB comprimido, ver nginx.conf) e depois fica em cache. Se não carregar (sem rede, aparelho
// antigo), devolve null e a câmera segue como antes, só com o oval, sem bloquear a captura.

// SIMD é suportado pela maioria dos aparelhos atuais; sem ele existe uma versão menor do runtime.
const BYTES_TESTE_SIMD = new Uint8Array([0, 97, 115, 109, 1, 0, 0, 0, 1, 5, 1, 96, 0, 1, 123, 3, 2, 1, 0, 10, 10, 1, 8, 0, 65, 0, 253, 15, 253, 98, 11]);

let promessa: Promise<FaceDetector | null> | null = null;

async function criar(): Promise<FaceDetector | null> {
  try {
    const simd = typeof WebAssembly !== 'undefined' && WebAssembly.validate(BYTES_TESTE_SIMD);
    const [{ FaceDetector }, loaderUrl, wasmUrl, modeloUrl] = await Promise.all([
      import('@mediapipe/tasks-vision'),
      simd
        ? import('@mediapipe/tasks-vision/vision_wasm_internal.js?url')
        : import('@mediapipe/tasks-vision/vision_wasm_nosimd_internal.js?url'),
      simd
        ? import('@mediapipe/tasks-vision/vision_wasm_internal.wasm?url')
        : import('@mediapipe/tasks-vision/vision_wasm_nosimd_internal.wasm?url'),
      import('../../assets/mediapipe/blaze_face_short_range.tflite?url'),
    ]);
    return await FaceDetector.createFromOptions(
      { wasmLoaderPath: loaderUrl.default, wasmBinaryPath: wasmUrl.default },
      {
        baseOptions: { modelAssetPath: modeloUrl.default, delegate: 'CPU' },
        runningMode: 'VIDEO',
        minDetectionConfidence: 0.6,
      },
    );
  } catch {
    return null;
  }
}

// Uma instância só para o app inteiro. Se a primeira tentativa falhar, a próxima abertura da câmera
// tenta de novo (a promessa em cache é descartada).
export function obterDetectorRosto(): Promise<FaceDetector | null> {
  promessa ??= criar().then((d) => {
    if (!d) promessa = null;
    return d;
  });
  return promessa;
}

export function detectarRostos(detector: FaceDetector, video: HTMLVideoElement, instanteMs: number): CaixaRosto[] {
  const resultado = detector.detectForVideo(video, instanteMs);
  return resultado.detections.flatMap((d) =>
    d.boundingBox
      ? [{ x: d.boundingBox.originX, y: d.boundingBox.originY, largura: d.boundingBox.width, altura: d.boundingBox.height }]
      : [],
  );
}
