// Porta única da camada ui/ (spec 2026-09-07 §2). Páginas importam daqui, nunca do Fluent direto.
export * from './tokens/tokens';
export * from './tokens/tipografia';
export * from './tokens/movimento';

// Primitivos Fluent que não ganham wrapper (spec §2.2) — re-exportados para a página não precisar
// importar @fluentui/react-components.
export {
  Avatar, Button, Checkbox, Field, Input, Select, Spinner, Text, Textarea, Tooltip,
} from '@fluentui/react-components';

// Peças já existentes, ainda no lugar antigo até a Onda 3.
export { CampoData } from '../components/CampoData';
export { ChipsField } from '../components/ChipsField';
