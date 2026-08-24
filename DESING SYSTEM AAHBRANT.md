# DESIGN SYSTEM - HUB ADMINISTRATIVO (GÊNESIS)
`DESIGN_SYSTEM.md` | Especificação Técnica e Guia de Estilo para Claude Code

---

## 1. Visão Geral e Princípios de Design

Este documento contém a especificação do Design System extraída do **Hub Administrativo Gênesis**. Utilize este arquivo como instrução mestre para a criação de interfaces web (React, Tailwind CSS, Vue ou HTML/CSS legados).

* **Tom e Estilo:** Corporativo, robusto, moderno, focado em alta densidade de informação e legibilidade para gestão de obras e engenharia.
* **Layout:** Sidebar fixa à esquerda (escuro), Header superior fixo (claro) e Área de Conteúdo fluida (cards em grid).
* **Bordas e Cantos:** Arredondados suavemente (`border-radius: 6px` a `8px`).
* **Sombras:** Sutis (`box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05)`).

---

## 2. Paleta de Cores (Design Tokens)

### Cores Institucionais
| Nome do Token | Código Hex | Uso Principal |
| :--- | :--- | :--- |
| `color-primary` | `#7B1E2B` | Marca principal, destaques ativos, branding |
| `color-secondary` | `#9E2A37` | Hover de botões, gradientes, variâncias |
| `color-neutral-dark` | `#1F1F1F` | Fundo da Sidebar, textos de alto contraste |
| `color-neutral-medium` | `#6D6D6D` | Subtítulos, rótulos de gráficos, bordas |
| `color-neutral-light` | `#F5F5F7` | Fundo principal da aplicação (`body-bg`) |
| `color-white` | `#FFFFFF` | Fundo de Cards, Modais, Header |

### Cores de Status e Feedback
| Status | Código Hex | Aplicação |
| :--- | :--- | :--- |
| `color-success` | `#16A34A` | Métricas positivas (↑), badges de concluído |
| `color-warning` | `#F59E0B` | Alertas médios, prazos próximos, em andamento |
| `color-alert` | `#EF4444` | Métricas negativas (↓), erros, atrasos críticos |
| `color-info` | `#3B82F6` | Links, ações neutras, gráficos informativos |

---

## 3. Tipografia

* **Fonte Principal (UI & Dados):** `Inter` (Weights: Light 300, Regular 400, Medium 500, Semi Bold 600, Bold 700)
* **Fonte Secundária (Títulos & Destaques):** `Poppins` (Weights: Regular 400, Medium 500, Semi Bold 600)

```css
/* Configuração Base de Tipografia */
body {
  font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
  color: #1F1F1F;
  background-color: #F5F5F7;
}

h1, h2, h3, .brand-title {
  font-family: 'Poppins', sans-serif;
}