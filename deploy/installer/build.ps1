<#
.SYNOPSIS
    FuelControl o'rnatuvchisini (Output\FuelControl-Setup-<versiya>.exe) yig'adi.

.DESCRIPTION
    1. Web:     npm run build                         -> src\backend\FuelControl.Api\wwwroot
    2. API:     publish (Release, win-x64, self-contained, wwwroot bilan)   -> build\stage\server
    3. Desktop: publish (Release, win-x64, self-contained)                  -> build\stage\desktop
    4. (ixtiyoriy) cloudflared.exe ni GitHub release'dan yuklash va imzosini tekshirish -> build\stage\tunnel
    5. Inno Setup (ISCC)                                                    -> Output\FuelControl-Setup-<versiya>.exe

    Versiya yagona manbadan olinadi: repo ildizidagi Directory.Build.props (<Version>).
    Publish natijalari --artifacts-path orqali build\artifacts ga tushadi: ishlab turgan dev API/Desktop (Debug bin)
    ning DLL'lari bilan to'qnashmaydi. build\ va Output\ .gitignore'da.

.PARAMETER SkipWeb
    Web'ni qayta build qilmaydi (wwwroot allaqachon tayyor bo'lsa).
.PARAMETER SkipCloudflared
    Cloudflare Tunnel komponentisiz yig'adi (cloudflared yuklanmaydi).
.PARAMETER SkipInstaller
    Faqat publish: build\stage to'ladi, ISCC ishga tushmaydi.
.PARAMETER Clean
    Boshlashdan oldin build\ papkasini (stage, artifacts, yuklangan fayllar) o'chiradi.
.PARAMETER IsccPath
    ISCC.exe yo'li (bo'lmasa: ISCC muhit o'zgaruvchisi, PATH, keyin Inno Setup 7 va 6 ning odatiy joylari).
.PARAMETER CloudflaredVersion
    'latest' yoki aniq reliz (masalan 2025.9.0).

.EXAMPLE
    .\build.ps1
.EXAMPLE
    .\build.ps1 -SkipWeb -SkipCloudflared
#>
[CmdletBinding()]
param(
    [switch] $SkipWeb,
    [switch] $SkipCloudflared,
    [switch] $SkipInstaller,
    [switch] $Clean,
    [string] $IsccPath,
    [string] $CloudflaredVersion = 'latest'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$Installer = $PSScriptRoot
$Repo      = (Resolve-Path (Join-Path $Installer '..\..')).Path
$Build     = Join-Path $Installer 'build'
$Stage     = Join-Path $Build 'stage'
$Artifacts = Join-Path $Build 'artifacts'
$Downloads = Join-Path $Build 'downloads'

function Step([string] $Text) { Write-Host ''; Write-Host "==> $Text" -ForegroundColor Cyan }

function Invoke-Native([string] $Exe, [string[]] $Arguments) {
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Exe xato bilan tugadi (kod $LASTEXITCODE): $($Arguments -join ' ')" }
}

# ---------- versiya (yagona manba) ----------
$propsPath = Join-Path $Repo 'Directory.Build.props'
[xml] $props = Get-Content -LiteralPath $propsPath -Raw
$versionNode = $props.SelectSingleNode('/Project/PropertyGroup/Version')
if (-not $versionNode -or -not $versionNode.InnerText.Trim()) { throw "Directory.Build.props ichida <Version> topilmadi: $propsPath" }
$Version = $versionNode.InnerText.Trim()
Write-Host "FuelControl o'rnatuvchisi, versiya $Version"

if ($Clean -and (Test-Path $Build)) {
    Step "build\ tozalanmoqda"
    Remove-Item -LiteralPath $Build -Recurse -Force
}
# Eski fayllar o'rnatuvchiga tushib qolmasin.
if (Test-Path $Stage) { Remove-Item -LiteralPath $Stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Stage, $Downloads | Out-Null

# ---------- 1) web ----------
$wwwroot = Join-Path $Repo 'src\backend\FuelControl.Api\wwwroot'
if (-not $SkipWeb) {
    Step "Web build (npm run build)"
    $web = Join-Path $Repo 'src\frontend\web\FuelControl.Web'
    Push-Location $web
    try {
        if (-not (Test-Path 'node_modules')) { Invoke-Native 'npm' @('ci') }
        Invoke-Native 'npm' @('run', 'build')
    }
    finally { Pop-Location }
}
if (-not (Test-Path (Join-Path $wwwroot 'index.html'))) {
    throw "Web build topilmadi ($wwwroot\index.html). -SkipWeb ni olib tashlang yoki avval 'npm run build' qiling."
}

# ---------- 2) API ----------
Step "API publish (Release, win-x64, self-contained)"
Invoke-Native 'dotnet' @(
    'publish', (Join-Path $Repo 'src\backend\FuelControl.Api\FuelControl.Api.csproj'),
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '--artifacts-path', $Artifacts, '-o', (Join-Path $Stage 'server'), '--nologo', '-v', 'minimal')

$server = Join-Path $Stage 'server'
foreach ($must in 'FuelControl.Api.exe', 'wwwroot\index.html', 'appsettings.json') {
    if (-not (Test-Path (Join-Path $server $must))) { throw "API publish to'liq emas: $must yo'q." }
}
# Dev kalit/parollar o'rnatuvchiga tushmasligi shart.
foreach ($mustNot in 'appsettings.Development.json', 'appsettings.Web.json', 'appsettings.Production.json', 'appsettings.Local.json') {
    if (Test-Path (Join-Path $server $mustNot)) { throw "API publish ichida bo'lmasligi kerak bo'lgan fayl bor: $mustNot" }
}

# ---------- 3) Desktop ----------
Step "Desktop publish (Release, win-x64, self-contained)"
Invoke-Native 'dotnet' @(
    'publish', (Join-Path $Repo 'src\frontend\desktop\FuelControl.Desktop\FuelControl.Desktop.csproj'),
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '--artifacts-path', $Artifacts, '-o', (Join-Path $Stage 'desktop'), '--nologo', '-v', 'minimal')
if (-not (Test-Path (Join-Path $Stage 'desktop\FuelControl.exe'))) { throw "Desktop publish to'liq emas: FuelControl.exe yo'q." }

# ---------- 4) cloudflared (ixtiyoriy) ----------
$defines = @()
if (-not $SkipCloudflared) {
    Step "cloudflared ($CloudflaredVersion)"
    $cfCache = Join-Path $Downloads "cloudflared-$CloudflaredVersion.exe"
    if (-not (Test-Path $cfCache)) {
        $cfUrl = if ($CloudflaredVersion -eq 'latest') {
            'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe'
        } else {
            "https://github.com/cloudflare/cloudflared/releases/download/$CloudflaredVersion/cloudflared-windows-amd64.exe"
        }
        Write-Host "Yuklanmoqda: $cfUrl"
        try { Invoke-WebRequest -Uri $cfUrl -OutFile $cfCache -UseBasicParsing }
        catch { throw "cloudflared yuklab bo'lmadi: $($_.Exception.Message). Internetsiz yig'ish uchun -SkipCloudflared ishlating." }
    }
    else { Write-Host "Keshdan: $cfCache" }

    if ((Get-Item $cfCache).Length -lt 10MB) { Remove-Item $cfCache -Force; throw "cloudflared fayli juda kichik — yuklash buzilgan. Qayta urining." }
    try {
        $sig = Get-AuthenticodeSignature -FilePath $cfCache
        if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'Cloudflare') {
            Remove-Item $cfCache -Force
            throw "cloudflared imzosi yaroqsiz yoki Cloudflare'niki emas (holat: $($sig.Status), imzolovchi: $($sig.SignerCertificate.Subject))."
        }
        Write-Host "Imzo: $($sig.SignerCertificate.Subject)"
    }
    catch [System.Management.Automation.CommandNotFoundException] { Write-Warning "Imzoni tekshirib bo'lmadi (Get-AuthenticodeSignature yo'q)." }
    Write-Host ("Versiya: " + ((& $cfCache --version) -join ' '))

    New-Item -ItemType Directory -Force -Path (Join-Path $Stage 'tunnel') | Out-Null
    Copy-Item -LiteralPath $cfCache -Destination (Join-Path $Stage 'tunnel\cloudflared.exe') -Force
    $defines += '/DTunnelDir=' + (Join-Path $Stage 'tunnel')
}

if ($SkipInstaller) {
    Step "Tayyor (faqat publish): $Stage"
    return
}

# ---------- 5) Inno Setup ----------
function Find-Iscc {
    if ($IsccPath) {
        if (Test-Path $IsccPath) { return (Resolve-Path $IsccPath).Path }
        throw "ISCC.exe topilmadi: $IsccPath"
    }
    if ($env:ISCC -and (Test-Path $env:ISCC)) { return $env:ISCC }
    $cmd = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $(if ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'Programs' })) | Where-Object { $_ }
    foreach ($v in '7', '6') {
        foreach ($r in $roots) {
            $p = Join-Path $r "Inno Setup $v\ISCC.exe"
            if (Test-Path $p) { return $p }
        }
    }
    throw "ISCC.exe (Inno Setup) topilmadi. O'rnating: winget install --id JRSoftware.InnoSetup.7 -e   (yoki -IsccPath bering)."
}

Step "Inno Setup (ISCC)"
$iscc = Find-Iscc
Write-Host "ISCC: $iscc"
$isccArgs = @(
    "/DAppVersion=$Version",
    "/DSourceRoot=$Repo",
    "/DServerDir=$server",
    "/DDesktopDir=$(Join-Path $Stage 'desktop')"
) + $defines + @((Join-Path $Installer 'FuelControl.iss'))
Invoke-Native $iscc $isccArgs

$exe = Join-Path $Installer "Output\FuelControl-Setup-$Version.exe"
if (-not (Test-Path $exe)) { throw "O'rnatuvchi yaratilmadi: $exe" }

Step "Tayyor"
$info = Get-Item $exe
Write-Host ("Fayl   : " + $info.FullName)
Write-Host ("Hajmi  : {0:N1} MB" -f ($info.Length / 1MB))
Write-Host ("SHA256 : " + (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash)
