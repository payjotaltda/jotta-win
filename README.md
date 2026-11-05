# JOTTA — cliente Windows

App nativo (WPF) para falar e digitar com o **JOTTA no storage-dev**: voz e texto vão para o Cursor no servidor; STT (Whisper) e orquestrador rodam lá.

## Download

Releases: [github.com/payjotaltda/jotta-win/releases](https://github.com/payjotaltda/jotta-win/releases) — baixe **`JOTTA.exe`**.

Ou, após push, em **Actions → Build JOTTA Windows → Artifacts**.

## No notebook (Windows)

1. Instale [Tailscale](https://tailscale.com/download/windows) e entre na mesma tailnet.
2. Rode **`JOTTA.exe`** — ícone na bandeja.
3. **Configurações** (menu do tray): URL do servidor (padrão `https://storage-dev.tailf8d872.ts.net:8443`).
4. Diga **JOTTA** (escuta em background) ou **duplo-clique** no tray → overlay com blur.
5. **Ctrl+Espaço** — chat texto · **Esc** — fecha · **clique no orbe** — segure e fale.

## No storage-dev (uma vez)

Expor a API do orbe só na tailnet:

```bash
tailscale serve --bg --https=8443 http://127.0.0.1:8086
tailscale serve status
```

Serviços user: `glance-agent-usage`, `glance-voice-orchestrator`.

## Build local (Windows)

```bash
dotnet publish JottaWin/JottaWin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Repositório

Código em `JottaWin/` — .NET 8, WPF + NAudio.
