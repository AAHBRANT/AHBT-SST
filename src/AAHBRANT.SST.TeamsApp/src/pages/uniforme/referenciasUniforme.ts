export const referenciasUniforme = [
  {
    "chave": "camisa-brim-laranja",
    "nome": "CAMISA BRIM MANGA LONGA",
    "cor": "Laranja",
    "grupo": "Direto",
    "funcoesIndicadas": [],
    "tecido": "Brim",
    "arquivo": "/uniformes/camisa-brim-laranja.jpeg",
    "frente": "/uniformes/camisa-brim-laranja-frente.jpg",
    "categoria": "Camisas"
  },
  {
    "chave": "camisa-brim-azul",
    "nome": "CAMISA BRIM MANGA LONGA",
    "cor": "Azul royal",
    "grupo": "Eletricista",
    "funcoesIndicadas": [
      "Eletricista"
    ],
    "tecido": "Brim",
    "arquivo": "/uniformes/camisa-brim-azul.jpeg",
    "frente": "/uniformes/camisa-brim-azul-frente.jpg",
    "categoria": "Camisas"
  },
  {
    "chave": "camisa-brim-marsala",
    "nome": "CAMISA BRIM MANGA LONGA",
    "cor": "Marsala",
    "grupo": "Direto",
    "funcoesIndicadas": [],
    "tecido": "Brim",
    "arquivo": "/uniformes/camisa-brim-marsala.jpeg",
    "frente": "/uniformes/camisa-brim-marsala-frente.jpg",
    "categoria": "Camisas"
  },
  {
    "chave": "camisa-feminina-marsala",
    "nome": "CAMISA FEMININA",
    "cor": "Marsala",
    "grupo": "Indireto · feminino",
    "funcoesIndicadas": [
      "Técnica de Segurança do Trabalho",
      "Técnica de Meio Ambiente",
      "Técnica de Qualidade",
      "Encarregada",
      "Professora Encarregada",
      "Engenheira"
    ],
    "tecido": "Tricoline",
    "arquivo": "/uniformes/camisa-feminina-marsala.jpeg",
    "frente": "/uniformes/camisa-feminina-marsala-frente.jpg",
    "categoria": "Camisas"
  },
  {
    "chave": "camisa-feminina-cinza",
    "nome": "CAMISA FEMININA",
    "cor": "Cinza claro",
    "grupo": "Indireto · feminino",
    "funcoesIndicadas": [
      "Técnica de Segurança do Trabalho",
      "Técnica de Meio Ambiente",
      "Técnica de Qualidade",
      "Encarregada",
      "Professora Encarregada",
      "Engenheira"
    ],
    "tecido": "Tricoline",
    "arquivo": "/uniformes/camisa-feminina-cinza.jpeg",
    "frente": "/uniformes/camisa-feminina-cinza-frente.jpg",
    "categoria": "Camisas"
  },
  {
    "chave": "calca-brim-laranja",
    "nome": "CALÇA BRIM",
    "cor": "Laranja",
    "grupo": "Direto",
    "funcoesIndicadas": [],
    "tecido": "Brim",
    "arquivo": "/uniformes/calca-brim-laranja.jpeg",
    "frente": "/uniformes/calca-brim-laranja-frente.jpg",
    "categoria": "Calças"
  },
  {
    "chave": "calca-brim-azul",
    "nome": "CALÇA BRIM",
    "cor": "Azul royal",
    "grupo": "Eletricista",
    "funcoesIndicadas": [
      "Eletricista"
    ],
    "tecido": "Brim",
    "arquivo": "/uniformes/calca-brim-azul.jpeg",
    "frente": "/uniformes/calca-brim-azul-frente.jpg",
    "categoria": "Calças"
  },
  {
    "chave": "calca-brim-marsala",
    "nome": "CALÇA BRIM",
    "cor": "Marsala",
    "grupo": "Direto",
    "funcoesIndicadas": [],
    "tecido": "Brim",
    "arquivo": "/uniformes/calca-brim-marsala.jpeg",
    "frente": "/uniformes/calca-brim-marsala-frente.jpg",
    "categoria": "Calças"
  },
  {
    "chave": "calca-brim-cinza",
    "nome": "CALÇA BRIM",
    "cor": "Cinza claro",
    "grupo": "Direto",
    "funcoesIndicadas": [],
    "tecido": "Brim",
    "arquivo": "/uniformes/calca-brim-cinza.jpeg",
    "frente": "/uniformes/calca-brim-cinza-frente.jpg",
    "categoria": "Calças"
  },
  {
    "chave": "agasalho-laranja",
    "nome": "AGASALHO IMPERMEÁVEL",
    "cor": "Laranja",
    "grupo": null,
    "funcoesIndicadas": [],
    "tecido": "Impermeável",
    "arquivo": "/uniformes/agasalho-laranja.jpeg",
    "frente": "/uniformes/agasalho-laranja-frente.jpg",
    "categoria": "Agasalhos"
  }
] as const;
export function referenciaUniforme(chave?: string | null) { return referenciasUniforme.find(r => r.chave === chave); }
