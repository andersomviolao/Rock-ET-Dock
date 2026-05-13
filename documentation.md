# Rock ET Dock - Documentacao

Rock ET Dock e um dock/app launcher para Windows criado em WPF. O projeto recria comportamentos de dock classico por implementacao limpa, sem depender dos binarios antigos do RocketDock em runtime.

## Estrutura

- `Dock.slnx`: solucao .NET.
- `src/Dock.App`: aplicativo WPF principal.
- `tests/Dock.GeometryChecks`: checks executaveis de geometria, reorder, importacao/exportacao e configuracoes.
- `assets`: assets proprios do projeto.
- `docs`: notas de pesquisa e requisitos.

Pastas locais de referencia como `_reference` e `_tools`, alem do instalador original `RocketDock-v1.3.5.exe`, ficam fora do Git.

## Requisitos

- Windows.
- .NET 10 SDK.
- PowerShell ou terminal equivalente.

## Comandos

```powershell
dotnet build Dock.slnx
dotnet run --project src\Dock.App\Dock.App.csproj
dotnet run --project tests\Dock.GeometryChecks\Dock.GeometryChecks.csproj
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

Tambem existe `run.bat` para iniciar o app a partir da raiz do projeto.

O artefato oficial de distribuicao e o instalador gerado em `artifacts\installer`. A pasta publicada em `artifacts\publish` e apenas entrada intermediaria do instalador.

## Dados do usuario

O app salva tudo por usuario:

- Configuracao: `%LOCALAPPDATA%\Rock ET Dock\dock.config.json`
- Logs: `%LOCALAPPDATA%\Rock ET Dock\logs\runtime.log`
- Pasta raiz gerenciada: `%USERPROFILE%\Rock ET Dock`
- Pasta de cada barra: `%USERPROFILE%\Rock ET Dock\<nome-da-barra>`

Para smoke tests sem tocar no perfil real, use:

```powershell
$env:ROCK_ET_DOCK_LOCALAPPDATA = "$env:TEMP\rock-et-dock-local"
$env:ROCK_ET_DOCK_USERPROFILE = "$env:TEMP\rock-et-dock-profile"
dotnet run --project src\Dock.App\Dock.App.csproj
```

## Funcionalidades atuais

- Multiplas barras por instancia.
- Posicionamento em topo, rodape, esquerda e direita.
- Configuracoes de tamanho, zoom, alcance de zoom, opacidade, espacamento e margens.
- Configuracoes de largura, altura, distancia da borda, centralizacao e layering.
- Temas inspirados nos nomes de skins de referencia, com estilos proprios.
- Sliders de arredondamento separados para a barra e fundo dos icones.
- Drag-and-drop para adicionar, reordenar e remover itens.
- Menu de contexto para adicionar arquivos, pastas, separadores, configuracoes e sair.
- Botao Windows com menu Iniciar no clique esquerdo e menu Win+X no clique direito.
- Lixeira com menu nativo e drop de arquivos.
- Opcao para esconder a barra nativa do Windows enquanto o app estiver aberto.
- Itens temporarios de janelas minimizadas.
- Hotkey global `Ctrl+Alt+R` para ocultar/exibir todas as barras abertas.
- Indicador de app aberto e abertura de instancia existente para itens `.exe` e atalhos `.lnk` resolviveis.

## Decisoes de implementacao

- O projeto e clean-room: comportamento e formatos foram documentados, mas codigo e assets de runtime sao proprios.
- Arquivos soltos na barra sao movidos ou referenciados por atalhos dentro da pasta gerenciada da barra, conforme a configuracao.
- A regra de mover/copiar entradas do sistema de arquivos fica centralizada em `ManagedPathService`; criacao e resolucao de atalhos ficam em `ShellShortcutService`.
- A janela de configuracoes salva e reflete mudancas imediatamente para permitir visualizar a barra enquanto se ajusta.
- O zoom usa interpolacao por frame para reduzir saltos visuais durante hover.
- Indicadores de execucao usam mapeamento best-effort por caminho de executavel. Documentos, URLs, apps modernos/UWP e comandos indiretos podem nao ser associados a uma janela existente.

## Validacao

Antes de publicar mudancas, rode:

```powershell
dotnet build Dock.slnx
dotnet run --project tests\Dock.GeometryChecks\Dock.GeometryChecks.csproj
```

Evite rodar build e checks em paralelo, porque a geracao WPF pode disputar arquivos temporarios em `obj`.
