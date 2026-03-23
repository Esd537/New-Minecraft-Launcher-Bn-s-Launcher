# Bn's Launcher

Launcher de Minecraft em WPF inspirado na ideia de launchers como o Freesm, com visual clean, pagina de versoes oficial, perfis, instancias separadas e personalizacao dentro do proprio app.

## O que ja esta funcionando

- Launcher Windows com interface customizavel, fullscreen `F11` e visual mais limpo.
- Escolha de versoes `release`, `snapshot`, `old_beta` e `old_alpha`.
- Download da versao oficial direto do catalogo da Mojang.
- Launch do Minecraft com sessao offline ou conta Microsoft e argumentos JVM otimizados por padrao.
- Loaders por instancia: `vanilla`, `forge`, `optifine` e `forge + optifine`.
- Perfis separados para nome, memoria, argumentos, cor e imagem de fundo.
- Temas prontos `preto + azul`, `preto + branco`, `preto + vermelho` e modo `custom`.
- Troca de tema e alteracoes do perfil sem crash ao salvar o preview.
- Logo propria `BN` aplicada no app e no instalador.
- Instancias com pastas separadas por padrao.
- Painel de noticias com feed oficial e fallback offline.
- Instalador com escolha de pasta, atalhos e abertura automatica no final.

## Onde esta o app

Pasta final pronta para abrir:

```text
Bn Launcher App v1.2.0
```

Executavel principal:

```text
Bn Launcher App v1.2.0\Bn's Launcher.exe
```

Pacote para GitHub Release:

```text
release\BnsLauncher-v1.2.0-win-x64.zip
```

Instalador unico para GitHub Release:

```text
release\BnsLauncher-Setup-v1.2.0.exe
```

## Como abrir

1. Entre na pasta `Bn Launcher App v1.2.0`.
2. Clique duas vezes em `Bn's Launcher.exe`.
3. No app, escolha `offline` ou `microsoft`.
4. Escolha o tema do launcher.
5. Clique em `Catalogo de versoes` e selecione a versao desejada.
6. Escolha `vanilla`, `forge`, `optifine` ou `forge + optifine`.
7. Se quiser, ajuste memoria, imagem de fundo e cor.
8. Pressione `F11` se quiser entrar em tela cheia.
9. Clique em `Iniciar Minecraft`.

Se o Java automatico nao funcionar em alguma maquina:

1. Instale Java 21.
2. No launcher, use o campo `Java`.
3. Selecione o arquivo `java.exe`.
4. Tente iniciar de novo.

## Como rodar pelo terminal

```powershell
& ".\Bn Launcher App v1.2.0\Bn's Launcher.exe"
```

## Como compilar de novo

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1"
$env:LOCALAPPDATA="$PWD\.localappdata"
$env:APPDATA="$PWD\.appdata"
$env:USERPROFILE="$PWD\.userprofile"
dotnet publish ".\Bn's Launcher\Bn's Launcher.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o ".\dist-clean"
```

Depois do publish, copie os arquivos de `dist-clean` para a pasta final que voce quiser distribuir.

## Como colocar no GitHub

1. Crie um repositorio novo no GitHub pelo site.
2. Copie a URL do repositorio.
3. Abra PowerShell na pasta raiz do projeto.
4. Rode os comandos abaixo.

```powershell
git add .
git commit -m "feat: add Bn's Launcher"
git branch -M main
git remote add origin https://github.com/SEU-USUARIO/SEU-REPO.git
git push -u origin main
```

Se o remoto ja existir:

```powershell
git remote set-url origin https://github.com/SEU-USUARIO/SEU-REPO.git
git push -u origin main
```

## Como publicar a release no GitHub

1. Entre no repositorio no site do GitHub.
2. Clique em `Releases`.
3. Clique em `Draft a new release`.
4. Crie a tag, por exemplo `v1.0.0`.
5. Em `Attach binaries`, envie `release\BnsLauncher-Setup-v1.2.0.exe` se quiser postar so o instalador.
6. Se preferir versao portatil, envie `release\BnsLauncher-v1.2.0-win-x64.zip`.
7. Cole as notas da release de `release\RELEASE_NOTES_v1.2.0.md`.
8. Clique em `Publish release`.

## Observacao

O projeto ignora binarios no `.gitignore`, entao o normal e subir so o codigo para o repositorio e usar o `.zip` da pasta `release` apenas na area de Releases do GitHub.
