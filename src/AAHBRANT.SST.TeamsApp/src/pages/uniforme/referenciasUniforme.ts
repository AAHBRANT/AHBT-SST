export const referenciasUniforme = [
  {
    "chave": "camisa-brim-laranja",
    "nome": "CAMISA BRIM MANGA LONGA",
    "cor": "Laranja",
    "grupo": "Direto",
    "funcoesIndicadas": [],
    "tecido": "Brim",
    "arquivo": "/uniformes/camisa-brim-laranja.jpeg",
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
    "categoria": "Agasalhos"
  }
] as const;
export function referenciaUniforme(chave?: string | null) { return referenciasUniforme.find(r => r.chave === chave); }
