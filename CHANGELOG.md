# Changelog

Todas as mudancas relevantes deste projeto serao documentadas aqui.

## 0.2.0 - 2026-05-13

- Refatoradas operacoes de mover/copiar arquivos e pastas para `ManagedPathService`.
- Extraida criacao/resolucao de atalhos `.lnk` para `ShellShortcutService`.
- Adicionada hotkey global `Ctrl+Alt+R` para ocultar/exibir todas as barras abertas.
- Adicionados indicadores de app aberto e tentativa de abrir instancia existente para itens `.exe` e atalhos `.lnk` resolviveis.
- Adicionados itens especiais persistentes de separador, configuracoes e sair.
- Adicionado menu de contexto para incluir arquivo, pasta, separador, configuracoes e sair.
- Barras novas agora iniciam com botao Windows, lixeira, configuracoes e sair.
- Removida a logica de aplicacao manual da janela de configuracoes; mudancas agora sao salvas e refletidas imediatamente.
- Corrigido zoom de hover para deslocar os icones vizinhos e evitar sobreposicao visual.
- Corrigido controle de GIF para retomar o loop quando o item estiver carregado e visivel.
- Definida versao do app como `0.2.0` para publicacao inicial.
- Adicionados checks para itens especiais e mapeamento de executavel.

## 0.1.0 - 2026-05-13

- Criado app WPF inicial do Rock ET Dock.
- Adicionada barra transparente com posicionamento em quatro bordas.
- Adicionados itens de arquivo, pasta, link, GIF animado, botao Windows e lixeira.
- Adicionado drag-and-drop para importar, reordenar, exportar para a area de trabalho e remover itens.
- Adicionada janela de configuracoes com aplicacao imediata.
- Adicionadas configuracoes de tamanho de icone, zoom, alcance, opacidade, espacamento, margens, largura, altura, posicionamento e temas.
- Adicionado hover com ampliacao suave por interpolacao.
- Adicionado suporte inicial a multiplas barras.
- Adicionados checks executaveis de geometria, reorder, importacao/exportacao, placeholder e sizing.
- Documentada a pesquisa clean-room sobre RocketDock em `docs/rocketdock-recreation-notes.md`.
