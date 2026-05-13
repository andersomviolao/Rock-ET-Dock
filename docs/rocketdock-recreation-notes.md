# RocketDock Recreation Notes

Data da coleta: 2026-05-12

## Objetivo

Recriar o Rock ET Dock, um dock/app launcher moderno inspirado no RocketDock, usando engenharia reversa comportamental e informacao publica, sem copiar codigo proprietario nem depender dos binarios antigos em runtime.

## Artefatos locais analisados

- Instalador original: `F:\scripts\GitHub\Dock\RocketDock-v1.3.5.exe`
- SHA-256 do instalador: `43759B0C441FD4F71FE5EEB69F548CD2EB40AC0ABFA02EA3AFC44FBDDF28DC16`
- Metadados do instalador: Inno Setup, `RocketDock Setup`, empresa `Punk Software`, sem assinatura Authenticode.
- Extracao local sem executar o instalador: `F:\scripts\GitHub\Dock\_reference\RocketDock-1.3.5\app`
- Ferramenta usada para extrair Inno Setup: `F:\scripts\GitHub\Dock\_tools\innoextract-1.9-windows\innoextract.exe`

Hashes dos binarios extraidos:

| Arquivo | SHA-256 |
| --- | --- |
| `RocketDock.exe` | `9FF98D6FD2539CEFC9F42103A7F72388BED6EE590400559B92BC7430228DA36A` |
| `RocketDock.dll` | `66704CE3EB7E723BA20D1AB7036AD0BA9E0A94261B7E66636B01DC76DEFEDB9D` |
| `Docklets\RocketClock\RocketClock.dll` | `ED736C6039DE8FCA833CEDE2B41474882DFF2647DECA3F12903BACC760864BEF` |

## Fontes publicas consultadas

- `https://punklabs.com/rocketdock?lang=en`
- `https://punklabs.com/content/projects/rocketdock/help/English/index.html?lang=en`
- `https://punklabs.itch.io/rocketdock`
- `https://winget.run/pkg/PunkLabs/RocketDock`
- `https://en.wikipedia.org/wiki/RocketDock`
- `https://constexpr.org/innoextract/`

## Restricoes legais e praticas

- O arquivo local `License.rtf` contem uma licenca Creative Commons com elementos `Attribution`, `Noncommercial` e `ShareAlike`.
- O pacote winget de terceiros lista a licenca como `Proprietary`, enquanto a pagina oficial atual nao detalha a licenca. Tratar isso como inconsistente ate decisao explicita.
- Para um app novo, a rota mais segura e clean-room:
  - documentar comportamento e formatos;
  - implementar codigo novo;
  - recriar ou substituir skins/icones por assets proprios ou claramente licenciados;
  - usar assets do RocketDock apenas como referencia local, ou somente se a licenca final aceitar as restricoes de atribuicao, uso nao comercial e share-alike.

## Inventario da instalacao extraida

| Pasta | Conteudo relevante |
| --- | --- |
| `Data` | imagens da UI de configuracao |
| `Defaults` | icone desconhecido, indicador de app aberto, efeito poof e skin padrao |
| `Docklets` | `Defaults.ini` e docklet `RocketClock` |
| `Help` | documentacao HTML e screenshots em varios idiomas |
| `Icons` | 22 icones PNG padrao |
| `Languages` | 51 arquivos `.ini` de traducao |
| `Skins` | 30 temas, cada um com PNG de fundo, PNG de separador e INIs |
| `Tools` | `Debug.exe` e `LanguageID Finder.exe` |

## Funcionalidades essenciais do RocketDock

Funcionalidades de produto que devem virar requisitos:

- Dock em uma borda da tela com fundo, separadores e linha de icones.
- Itens adicionaveis por drag-and-drop e menu contextual:
  - arquivo;
  - pasta;
  - icone em branco configuravel;
  - separador;
  - lixeira;
  - configuracoes do dock;
  - sair.
- Reordenacao de itens por arrastar.
- Remocao de itens arrastando para fora ou por menu.
- Drop de arquivos sobre itens:
  - sobre app: abrir app passando arquivo/pasta como argumento;
  - sobre pasta: copiar para a pasta;
  - sobre lixeira: apagar.
- Minimizacao de janelas para o dock.
- Previews em tempo real de janelas minimizadas quando DWM estiver disponivel.
- Indicador visual para apps em execucao.
- Abrir instancia existente do app em vez de iniciar nova instancia, com override por item.
- Suporte multi-monitor.
- Posicoes: topo, rodape, esquerda, direita.
- Layering: sempre acima, normal, sempre abaixo.
- Centralizacao e offset em relacao a borda.
- Auto-hide e popup on mouseover com delay/duracao.
- Zoom suave de icones, largura de efeito, duracao e efeito hover.
- Efeito de atencao ao clicar ou quando janela pisca.
- Opacidade global de icones e fundo.
- Labels com fonte, cor, sombra, contorno e opacidade.
- Bloqueio de itens.
- Hotkey global `Ctrl+Alt+R` para ocultar/exibir o dock.
- Modo portavel via `Settings.ini`.
- Persistencia normal via `HKCU\Software\RocketDock`.
- Traducao por arquivos `.ini`.
- Compatibilidade com skins de MobyDock, ObjectDock, RK Launcher e Y'z Dock.
- Suporte a docklets do ObjectDock.

## Configuracoes e formatos

### Skins

O formato de skin extraido e simples e bom candidato para compatibilidade:

`background.ini`:

```ini
[Background]
Image        = bg.png
LeftMargin   = 8
TopMargin    = 8
RightMargin  = 8
BottomMargin = 8
Outside-LeftMargin   = 20
Outside-TopMargin    = 13
Outside-RightMargin  = 23
Outside-BottomMargin = 14
```

`separator.ini`:

```ini
[Separator]
Image = sep.png
TopMargin = 3
BottomMargin = 3
```

Interpretacao recomendada:

- `Image`: PNG relativo a pasta do tema.
- `Left/Top/Right/BottomMargin`: margens internas para 9-slice rendering.
- `Outside-*`: margens externas usadas para encaixar background em volta da linha de icones.
- Separador: imagem PNG escalada/posicionada verticalmente usando margens superior/inferior.

### Docklets

`Docklets\Defaults.ini` tem defaults para docklets conhecidos:

```ini
[Docklets\Weather\WeatherDocklet.dll]
ForceDockletDefaults=1
ZipCode=86001
ImagesFolder=icons
UseMetric=0

[Docklets\RecycleBin\RecycleBin.dll]
ForceDockletDefaults=1
ImgEmpty=Icons\Recycle Bin.png
ImgFull=Icons\Recycle Bin (full).png
```

Strings no `RocketClock.dll` indicam os callbacks de docklet esperados:

- `OnCreate`
- `OnDestroy`
- `OnSave`
- `OnConfigure`
- `OnLeftButtonClick`
- `OnDoubleClick`
- `OnDropFiles`
- `OnAcceptDropFiles`
- `OnProcessMessage`
- `OnGetInformation`

Strings no docklet tambem indicam funcoes auxiliares esperadas pelo host:

- `DockletSetLabel`
- `DockletSetImageFile`
- `DockletSetImageOverlay`
- `DockletBrowseForImage`
- `DockletDoAttentionAnimation`
- `DockletGetRelativeFolder`

Recomendacao: para MVP, nao carregar DLLs antigas. Definir uma API propria de plugins, e deixar a compatibilidade ObjectDock/RocketDock como fase posterior, preferencialmente em processo isolado.

### Idiomas

Arquivos em `Languages\*.ini` usam secao `[Translation]` e pares chave-valor. O portugues do Brasil esta em `Languages\1046.ini`.

Exemplo:

```ini
[Translation]
ThisLanguage=Portugues do Brasil
DockSettings=Configuracoes do Dock
IconSettings=Propriedades do Icone
AddItem=Adicionar Item
```

Recomendacao: preservar importador desse formato, mas salvar traducoes novas em JSON/TOML se isso simplificar o app moderno.

## APIs Win32 observadas em strings/imports

O `RocketDock.exe` contem referencias a estas areas de API:

- Shell/start:
  - `ShellExecuteW`
  - `ShellExecuteExW`
  - `Software\Microsoft\Windows\CurrentVersion\Run`
- Persistencia/config:
  - `Software\RocketDock`
  - `Settings.ini`
  - `Docklets\Settings.ini`
- Janela/posicionamento:
  - `CreateWindowExW`
  - `UpdateLayeredWindow`
  - `SetWindowPos`
  - `GetWindowRect`
  - `WindowFromPoint`
  - `EnumDisplayMonitors`
- Hooks/hotkeys:
  - `RegisterHotKey`
  - `UnregisterHotKey`
  - `RegisterShellHookWindow`
  - `SetWindowsHookExW`
  - `UnhookWindowsHookEx`
- DWM/previews:
  - `DwmIsCompositionEnabled`
  - `DwmRegisterThumbnail`
  - `DwmUpdateThumbnailProperties`
  - `DwmUnregisterThumbnail`
  - `PrintWindow`
- Rendering:
  - `gdiplus.dll`
  - varias funcoes `Gdip*`, incluindo draw image, texture, font, string measure, compositing e smoothing.
- Icones:
  - `CreateIconFromResourceEx`
  - `LookupIconIdFromDirectoryEx`
  - `ExtractIcon` equivalente deve ser considerado no clone.

Arquitetura moderna equivalente:

- Windows nativo: WinUI 3/WPF + interop Win32 para DWM, shell hooks, hotkey e app activation.
- Alternativa cross-platform: Tauri/React ou Electron com modulo nativo para Windows. Menos ideal se o foco for baixa latencia e integracao profunda com shell.
- Recomendacao pratica para clone fiel no Windows: WPF ou WinUI 3 com Direct2D/Composition, persistencia em `%APPDATA%` ou `%LOCALAPPDATA%`, e importadores para `Settings.ini`, skins e idiomas.

## Modelo de dados recomendado

```ts
type DockItemKind =
  | "file"
  | "folder"
  | "blank"
  | "separator"
  | "recycleBin"
  | "dockSettings"
  | "quit"
  | "window";

interface DockItem {
  id: string;
  kind: DockItemKind;
  name: string;
  target?: string;
  workingDirectory?: string;
  arguments?: string;
  runMode?: "normal" | "minimized" | "maximized";
  iconPath?: string;
  iconState?: "normal" | "empty" | "full";
  openRunning?: "global" | "always" | "never";
  showPopupMenu?: boolean;
  locked?: boolean;
}

interface DockSettings {
  language: string;
  runAtStartup: boolean;
  portableIni: boolean;
  minimizeWindowsToDock: boolean;
  disableMinimizeAnimations: boolean;
  showRunningIndicators: boolean;
  openRunningGlobal: boolean;
  lockItems: boolean;
  iconQuality: "low" | "medium" | "high";
  iconOpacity: number;
  zoomOpaque: boolean;
  iconSize: number;
  hoverEffect: "none" | "bubble" | "plateau";
  zoomSize: number;
  zoomWidth: number;
  zoomDurationMs: number;
  monitor: string | number | "all";
  edge: "top" | "bottom" | "left" | "right";
  layering: "topmost" | "normal" | "bottom";
  centering: number;
  edgeOffset: number;
  theme: string;
  backgroundOpacity: number;
  hideIconLabels: boolean;
  font: {
    family: string;
    size: number;
    style: string;
    color: string;
    shadowColor: string;
    outlineColor: string;
    outlineOpacity: number;
    shadowOpacity: number;
  };
  attentionEffect: string;
  autoHide: boolean;
  autoHideDurationMs: number;
  autoHideDelayMs: number;
  popupOnMouseover: boolean;
  popupDelayMs: number;
}
```

## MVP sugerido

1. Janela sem borda, transparente e clicavel, ancorada em uma borda do monitor.
2. Renderizacao horizontal e vertical de icones PNG/ICO com labels e separadores.
3. Importador de skins `background.ini`/`separator.ini`.
4. Modelo persistente de itens e configuracoes em JSON, com export/import para INI depois.
5. Drag-and-drop para adicionar, reordenar, remover e abrir arquivos sobre itens.
6. Launch de arquivo/pasta/app com `ShellExecuteEx`.
7. Auto-hide, popup on mouseover, topmost/normal/bottom e hotkey `Ctrl+Alt+R`.
8. Indicador de app rodando por mapeamento de processos/janelas.
9. Lixeira com estados vazio/cheio.
10. Minimizacao de janelas para o dock e previews DWM.
11. UI de configuracoes completa.
12. Importador de idiomas `.ini`.
13. Plugins/docklets em API nova.
14. Compatibilidade parcial com docklets ObjectDock somente se ainda for necessaria.

## Testes de comportamento pendentes

Esses pontos exigem execucao controlada, idealmente em VM/sandbox:

- Capturar `Settings.ini` real criado pelo RocketDock ao ativar modo portavel.
- Comparar defaults de instalacao limpa contra `HKCU\Software\RocketDock`.
- Gravar coordenadas reais de layout para cada `edge`, `centering`, `edgeOffset`, zoom e separator.
- Medir curva de zoom/animacao e duracao real.
- Confirmar como `Open Running Application Instance` associa item com janela/processo.
- Confirmar ordem de prioridade de icone: PNG customizado, ICO, recurso de EXE/DLL, default unknown.
- Confirmar comportamento com UWP/Windows Apps modernos.
- Confirmar compatibilidade real com skins ObjectDock/MobyDock/RK Launcher/Y'z Dock.
- Confirmar ABI de docklets ObjectDock antes de qualquer suporte a DLLs antigas.

## Decisoes tecnicas recomendadas

- Nao iniciar por decompilacao do `RocketDock.exe`. O caminho mais util e menos arriscado e comportamento + formatos + implementacao limpa.
- Usar os arquivos extraidos apenas como corpus de referencia e testes de importacao.
- Priorizar compatibilidade de skin e itens antes de docklets. Docklets antigos aumentam risco de crash e exigem ABI legado em processo 32-bit.
- Manter o clone 64-bit, mas considerar um host 32-bit separado se suporte a docklets antigos virar requisito.
- Salvar configuracoes novas fora da pasta do app por padrao, em `%LOCALAPPDATA%`, e oferecer modo portavel explicito.
- Criar um pacote de assets proprio antes de qualquer distribuicao.

## Decisoes iniciais do Rock ET Dock

- Nome do app: `Rock ET Dock`.
- Pasta por usuario: `%USERPROFILE%\Rock ET Dock`.
- Configuracao por usuario: `%LOCALAPPDATA%\Rock ET Dock\dock.config.json`.
- Cada barra tem pasta propria dentro da pasta raiz do usuario.
- Arquivos, pastas e links soltos na barra sao movidos/criados dentro da pasta da barra.
- Uma unica instancia do app deve gerenciar multiplas barras configuraveis.
- Simbolo inicial: cabeca de alien com bandana vermelha em vetor proprio, sem reutilizar assets do RocketDock.
- O botao Windows e um item especial persistente da barra: clique esquerdo aciona o menu Iniciar nativo e clique direito aciona o menu nativo Win+X.
- A opcao de esconder a barra nativa do Windows e aplicada apenas durante a sessao do app e restaurada ao sair.
- O backend inicial de minimizar janelas usa WinEvent hooks para criar itens temporarios de janela na primeira barra aberta.
