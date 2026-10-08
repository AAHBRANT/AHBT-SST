export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7095';

// API do Banco de Ideias. Roda num Container App próprio (Hospedagem__Modo=Ideias, ver docs/banco-de-ideias.md);
// sem VITE_IDEIAS_API_BASE_URL cai na API principal, que continua expondo o módulo no modo Completo.
export const IDEIAS_API_BASE_URL: string = import.meta.env.VITE_IDEIAS_API_BASE_URL || API_BASE_URL;
