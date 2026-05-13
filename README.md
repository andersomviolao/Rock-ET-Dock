# Rock ET Dock

Workspace para recriar um dock/app launcher moderno inspirado no RocketDock.

## Projeto base

- Solucao: `Dock.slnx`
- App WPF: `src/Dock.App/Dock.App.csproj`
- Target: `.NET 10`, `net10.0-windows`
- Build: `dotnet build Dock.slnx`
- Run: `dotnet run --project src/Dock.App/Dock.App.csproj`
- Release: `powershell -ExecutionPolicy Bypass -File installer/build-installer.ps1`
- Validar geometria das quatro bordas: `dotnet run --project tests/Dock.GeometryChecks/Dock.GeometryChecks.csproj`

## Contrato de dados

Esta dock foi desenhada para ser sempre por usuario:

- Configuracao: `%LOCALAPPDATA%\Rock ET Dock\dock.config.json`
- Pasta raiz do usuario: `%USERPROFILE%\Rock ET Dock`
- Pasta de cada barra: `%USERPROFILE%\Rock ET Dock\<nome-da-barra>`
- Log de runtime: `%LOCALAPPDATA%\Rock ET Dock\logs\runtime.log`

Quando arquivos, pastas ou links sao soltos na barra, o item deve ser movido/criado dentro da pasta da barra e a dock passa a apontar para essa copia gerenciada. Isso e intencionalmente diferente do RocketDock original.

O app tambem foi desenhado para uma unica aplicacao gerenciar varias barras. Cada barra tem nome, posicao e pasta propria, entao o usuario pode manter, por exemplo, uma barra na esquerda e outra na direita sem depender de multiplas instalacoes.

## Configuracoes

A janela de configuracoes fica no menu de contexto da barra, em `Configuracoes da barra`.

As categorias iniciais seguem a organizacao da RocketDock:

- Geral: nome, inicializacao, labels, bloqueio e auto-hide.
- Geral tambem inclui o botao Windows e a opcao de esconder a barra nativa do Windows enquanto o Rock ET Dock estiver aberto.
- Icones: tamanho, opacidade, espacamento, margem inferior, qualidade, zoom, alcance da ampliacao e hover.
- Posicao: monitor, lado da tela, sobreposicao, largura, altura, distancia da borda e centralizacao com marcador de centro.
- Estilo: temas da referencia RocketDock, opacidade do fundo, arredondamento da barra, arredondamento do fundo dos icones, fonte e cor da legenda.
- Comportamento: minimizar janelas, indicadores, instancia existente e popup.

As configuracoes sao salvas e refletidas na barra imediatamente, sem botao manual de aplicacao. O botao Windows abre o menu Iniciar no clique esquerdo e o menu nativo Win+X no clique direito. O backend de minimizar janelas para a barra usa eventos Win32 e adiciona itens temporarios de janela na primeira barra aberta.

Os indicadores de apps abertos e a opcao de abrir instancia existente funcionam em modo best-effort para itens `.exe` e atalhos `.lnk` resolviveis. Itens que abrem documentos, URLs, apps modernos/UWP ou comandos indiretos podem nao mapear para um processo existente.

O simbolo inicial do projeto fica em `assets/rock-et-dock-logo.svg` e tambem existe como controle WPF em `src/Dock.App/Controls/BrandLogo.xaml`.

## Pesquisa inicial

- Notas de engenharia reversa e requisitos: [`docs/rocketdock-recreation-notes.md`](docs/rocketdock-recreation-notes.md)
- Instalador original de referencia: `RocketDock-v1.3.5.exe`
- Extracao local do instalador: `_reference/RocketDock-1.3.5/app`
- Ferramenta local usada para extrair Inno Setup: `_tools/innoextract-1.9-windows/innoextract.exe`

## Direcao recomendada

Implementar um clone clean-room: usar comportamento, formatos e documentacao como referencia, mas escrever codigo novo e evitar reutilizar codigo/assets do RocketDock em uma distribuicao nova ate a licenca ser decidida.

Primeiro MVP:

1. Janela transparente sem borda ancorada na borda da tela.
2. Itens de arquivo/pasta/separador/lixeira/configuracoes/sair.
3. Drag-and-drop e menu de contexto para adicionar, reordenar e remover.
4. Importador de skins `background.ini` e `separator.ini` (pendente).
5. Launch por `ShellExecuteEx`, auto-hide, topmost e hotkey `Ctrl+Alt+R`.
6. Indicador de app em execucao para `.exe`/`.lnk` e abertura de instancia existente quando ha janela visivel.
7. Minimizacao de janelas e previews DWM depois do dock basico estar solido.

Estado atual adicional:

- Barras novas ja nascem com botao Windows, lixeira, configuracoes e sair.
- `Ctrl+Alt+R` oculta/exibe todas as barras abertas.
- Operacoes de mover/copiar itens gerenciados e criar/ler atalhos ficam centralizadas nos servicos, para evitar regras duplicadas entre importacao, exportacao e runtime.
- Ainda ficam fora desta etapa: previews DWM, docklets legados e renderizacao fiel de skins antigas com 9-slice.
