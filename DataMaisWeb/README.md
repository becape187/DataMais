# DataMais Web - Sistema de Testes Hidráulicos

Aplicação web React para controle e monitoramento de máquinas de teste hidráulicas.

## 🎨 Design

O sistema utiliza as cores da MODEC:
- **Vermelho**: #E31E24
- **Azul Marinho**: #003366

## 🚀 Tecnologias

- **React 19** com TypeScript
- **Vite** - Build tool
- **React Router** - Roteamento
- **Recharts** - Gráficos em tempo real
- **Axios** - Cliente HTTP

## 📦 Instalação

```bash
npm install
```

## 🏃 Executar

```bash
npm run dev
```

O servidor de desenvolvimento estará disponível em `http://localhost:5173`

## 📁 Estrutura do Projeto

```
src/
├── components/     # Componentes reutilizáveis
│   └── Layout/     # Layout principal com sidebar
├── pages/          # Páginas da aplicação
│   ├── Dashboard.tsx
│   ├── ControleHidraulico.tsx
│   ├── Ensaio.tsx
│   ├── Clientes.tsx
│   ├── Sensores.tsx
│   └── ConfiguracaoSensor.tsx
└── App.tsx         # Componente principal
```

## 🎯 Funcionalidades

- ✅ Dashboard com visão geral do sistema
- ✅ Controle da unidade hidráulica (motor e cilindro)
- ✅ Ensaio em tempo real com gráfico de pressão
- ✅ Cadastro de clientes
- ✅ Cadastro e configuração de sensores
- ✅ Upload de certificados de calibração
- ✅ Configuração de pontos de correção de curva

## 🎨 Características do Design

- Interface moderna e responsiva
- Cores da marca MODEC (vermelho e azul marinho)
- Gráficos em tempo real com Recharts
- Sidebar com navegação intuitiva
- Cards e componentes visuais modernos

