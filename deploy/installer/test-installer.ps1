#requires -Version 7
<#
.SYNOPSIS
    FuelControl o'rnatuvchisining administratorsiz sinovi (yig'ilgan Output\FuelControl-Setup-<versiya>.exe ustida).

.DESCRIPTION
    O'rnatuvchi /CURRENTUSER va /NOSYSTEM=1 bilan ishga tushiriladi: Windows xizmati, xavfsizlik devori, ruxsatlar va
    cloudflared xizmati tegilmaydi (ularni qo'lda sinash: docs\ornatish.md, 16-bo'lim). Tekshiriladi:
      1. noto'g'ri qiymatlarda o'rnatuvchi hech narsa o'rnatmay chiqadi;
      2. birinchi o'rnatish: fayllar, appsettings.Production.json (port, yo'llar, JWT kaliti, admin paroli), versiya;
      3. o'rnatilgan haqiqiy API Production rejimida (127.0.0.1:<port>) ishga tushadi: /openapi, "/" (PWA), admin login, jurnal;
         toza baza: faqat admin (/me), yoqilg'i/aparat/smena/nasiya yo'q, aparatsiz smena ochib bo'lmaydi (demo ma'lumot yozilmaydi);
      4. yangilash: JWT kaliti saqlanadi, parol so'ralmaydi, appsettings.Local.json (CORS) saqlanadi, baza saqlanadi;
      5. faqat desktop; 6. tunnel komponenti (token bilan/tokensiz); 7. o'chirish: ma'lumotlar saqlanadi.
    Sinov vaqtinchalik papkada ishlaydi; muvaffaqiyatli tugasa u o'chiriladi. Xato bo'lsa o'rnatuvchi jurnallari qoladi.
    Bu mashinada FuelControl allaqachon o'rnatilgan bo'lsa (HKCU/HKLM), sinov to'xtaydi — o'rnatilishni buzmaslik uchun.

.PARAMETER Setup
    Sinaladigan o'rnatuvchi (standart: Output\FuelControl-Setup-<versiya>.exe, versiya Directory.Build.props dan).
.PARAMETER WorkDir
    Vaqtinchalik ish papkasi (nomida "fuelcontrol" bo'lishi shart).
.PARAMETER BasePort
    Sinov uchun birinchi port; BasePort..BasePort+3 band bo'lmasligi kerak.
.PARAMETER SkipTunnel
    O'rnatuvchi cloudflared'siz yig'ilgan bo'lsa (build.ps1 -SkipCloudflared), tunnel sinovini o'tkazib yuboradi.
.PARAMETER KeepFiles
    Muvaffaqiyatli tugaganda ham ish papkasini o'chirmaydi.

.EXAMPLE
    .\test-installer.ps1
#>
[CmdletBinding()]
param(
    [string] $Setup,
    [string] $WorkDir = (Join-Path ([IO.Path]::GetTempPath()) 'fuelcontrol-installer-test'),
    [int] $BasePort = 5090,
    [switch] $SkipTunnel,
    [switch] $KeepFiles
)

$ErrorActionPreference = 'Stop'
$Installer = $PSScriptRoot
$Repo = (Resolve-Path (Join-Path $Installer '..\..')).Path
[xml] $props = Get-Content -LiteralPath (Join-Path $Repo 'Directory.Build.props') -Raw
$Version = $props.SelectSingleNode('/Project/PropertyGroup/Version').InnerText.Trim()
if (-not $Setup) { $Setup = Join-Path $Installer "Output\FuelControl-Setup-$Version.exe" }
if (-not (Test-Path -LiteralPath $Setup)) { throw "O'rnatuvchi topilmadi: $Setup (avval build.ps1)." }
$T = $WorkDir
if ((Split-Path $T -Leaf) -notmatch 'fuelcontrol') { throw "WorkDir nomida 'fuelcontrol' bo'lishi shart (tasodifan boshqa papkani o'chirmaslik uchun): $T" }

$script:fail = 0
$script:apiProc = $null
$env:ASPNETCORE_ENVIRONMENT = 'Production'; $env:DOTNET_ENVIRONMENT = 'Production'

function Fresh([string] $p) { if (Test-Path -LiteralPath $p) { [IO.Directory]::Delete($p, $true) }; [void](New-Item -ItemType Directory -Force -Path $p) }
function Check($cond, [string] $msg) {
    $ok = if ($cond -is [array]) { @($cond).Count -gt 0 } else { [bool] $cond }
    if ($ok) { "  OK    $msg" } else { "  XATO  $msg"; $script:fail++ }
}
function Json([string] $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }

function Run-Setup([string[]] $Extra, [string] $LogName) {
    $a = $Extra + @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/CURRENTUSER', '/NOSYSTEM=1', "/LOG=$T\$LogName.log")
    (Start-Process -FilePath $Setup -ArgumentList $a -Wait -PassThru).ExitCode
}

function Start-Api([string] $app, [int] $port) {
    # Xavfsizlik devori oynasi chiqmasligi uchun sinovda 0.0.0.0 o'rniga 127.0.0.1 (kalit "Urls" shu fayldan o'qilishi baribir tekshiriladi).
    $cfg = "$app\Server\appsettings.Production.json"
    (Get-Content -LiteralPath $cfg -Raw).Replace('http://0.0.0.0:', 'http://127.0.0.1:') | Set-Content -LiteralPath $cfg -NoNewline -Encoding utf8NoBOM
    $script:apiProc = Start-Process -FilePath "$app\Server\FuelControl.Api.exe" -WorkingDirectory "$app\Server" -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput "$T\api-out-$port.txt" -RedirectStandardError "$T\api-err-$port.txt"
    for ($i = 0; $i -lt 60; $i++) {
        try { if ((Invoke-WebRequest "http://127.0.0.1:$port/openapi/v1.json" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { return $true } } catch { }
        if ($script:apiProc.HasExited) { return $false }
        Start-Sleep -Seconds 1
    }
    return $false
}

# Faqat shu sinov ishga tushirgan jarayon to'xtatiladi (boshqa API'larga tegilmaydi).
function Stop-Api { if ($script:apiProc -and -not $script:apiProc.HasExited) { Stop-Process -Id $script:apiProc.Id -Force; $script:apiProc.WaitForExit(10000) | Out-Null }; $script:apiProc = $null }

function Login([int] $port, [string] $pwd) {
    $body = @{ login = 'admin'; parolYokiPin = $pwd } | ConvertTo-Json
    try { Invoke-RestMethod "http://127.0.0.1:$port/auth/login" -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) } catch { $null }
}

function Uninstall-All {
    foreach ($a in @("$T\app1", "$T\app2", "$T\app3", "$T\app4")) {
        if (Test-Path "$a\unins000.exe") { Start-Process -FilePath "$a\unins000.exe" -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait | Out-Null }
    }
}

# ---------- xavfsizlik: haqiqiy o'rnatilgan FuelControl'ni buzmaslik; portlar band emas ----------
$iss = Get-Content -LiteralPath (Join-Path $Installer 'FuelControl.iss') -Raw
$appId = [regex]::Match($iss, '(?m)^AppId=\{\{([0-9A-Fa-f-]{36})\}').Groups[1].Value
if (-not $appId) { throw "FuelControl.iss ichida AppId topilmadi." }
foreach ($hive in 'HKCU', 'HKLM') {
    foreach ($view in 'Software\Microsoft\Windows\CurrentVersion\Uninstall', 'Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall') {
        $k = "${hive}:\$view\{$appId}_is1"
        if (Test-Path $k) { throw "Bu mashinada FuelControl allaqachon o'rnatilgan ($k). Sinov uning ro'yxatdan o'tishini buzgan bo'lardi: avval uni o'chiring yoki toza mashinada ishga tushiring." }
    }
}
$busy = [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port
foreach ($pt in $BasePort..($BasePort + 3)) { if ($busy -contains $pt) { throw "Port $pt band. -BasePort bilan boshqasini tanlang." } }
$p1, $p2, $p3, $p4 = $BasePort..($BasePort + 3)

try {
    Write-Host "Sinov: $Setup (versiya $Version), ish papkasi: $T"
    Fresh $T
    $app1 = "$T\app1"; $data1 = "$T\data1"
    # Maxfiy parol: teskari chiziq, qavslar, |, vergul, %, apostrof va unicode (JSON/qator qochirish tekshiruvi).
    $pwd1 = "P@ss\w0rd{x}|y,z%1'" + [char]0xE9

    "== 1. Noto'g'ri qiymatlar (o'rnatuvchi hech narsa o'rnatmay chiqishi kerak) =="
    foreach ($case in @(
        @{ N = "port 99999";             A = @("/DIR=$T\bad1", "/DATADIR=$T\baddata", '/PORT=99999', '/ADMINPAROL=KuchliParol1', '/COMPONENTS=server,desktop') },
        @{ N = "qisqa parol";            A = @("/DIR=$T\bad2", "/DATADIR=$T\baddata", "/PORT=$p1", '/ADMINPAROL=short', '/COMPONENTS=server,desktop') },
        @{ N = "ftp:// server manzili";  A = @("/DIR=$T\bad3", "/DATADIR=$T\baddata", '/SERVERURL=ftp://x', '/COMPONENTS=desktop') },
        @{ N = "server siz tunnel";      A = @("/DIR=$T\bad4", "/DATADIR=$T\baddata", '/COMPONENTS=tunnel') },
        @{ N = "hech narsa tanlanmagan"; A = @("/DIR=$T\bad5", "/DATADIR=$T\baddata", '/COMPONENTS=') })) {
        $dir = ($case.A | Where-Object { $_ -like '/DIR=*' }).Substring(5)
        $code = Run-Setup $case.A ('bad-' + ($case.N -replace '\W', '_'))
        Check (($code -ne 0) -and -not (Test-Path "$dir\Server") -and -not (Test-Path "$dir\Desktop")) "$($case.N): chiqish kodi $code, fayl o'rnatilmadi"
    }

    "== 2. Birinchi o'rnatish: server + desktop, port $p1 =="
    $code = Run-Setup @("/DIR=$app1", "/DATADIR=$data1", "/PORT=$p1", "/ADMINPAROL=$pwd1", '/COMPONENTS=server,desktop', '/TASKS=') 'install1'
    Check ($code -eq 0) "o'rnatuvchi chiqish kodi 0 (kod $code)"
    foreach ($f in 'Server\FuelControl.Api.exe', 'Server\wwwroot\index.html', 'Server\appsettings.json', 'Server\appsettings.Production.json', 'Server\appsettings.Local.json',
                   'Desktop\FuelControl.exe', 'Desktop\sozlama.json', 'FuelControl Web.url', 'fuelcontrol.ico', 'nosystem.flag', 'unins000.exe') {
        Check (Test-Path "$app1\$f") "mavjud: $f"
    }
    foreach ($f in 'Server\appsettings.Development.json', 'Server\appsettings.Web.json', 'Tunnel\cloudflared.exe') {
        Check (-not (Test-Path "$app1\$f")) "YO'Q (bo'lmasligi kerak): $f"
    }
    Check ((Get-Item "$app1\Server\FuelControl.Api.exe").VersionInfo.ProductVersion -eq $Version) "API versiyasi $Version"
    Check ((Get-Item "$app1\Desktop\FuelControl.exe").VersionInfo.ProductVersion -eq $Version) "Desktop versiyasi $Version"
    Check ((Test-Path "$data1\zaxira") -and (Test-Path "$data1\logs")) "ma'lumot papkalari yaratildi (zaxira, logs)"
    $cfg = Json "$app1\Server\appsettings.Production.json"
    Check ($cfg.Urls -eq "http://0.0.0.0:$p1") "Urls = $($cfg.Urls)"
    Check ($cfg.ConnectionStrings.Baza -eq "Data Source=$data1\fuelcontrol.db") "baza yo'li: $($cfg.ConnectionStrings.Baza)"
    Check (($cfg.Zaxira.Papka -eq "$data1\zaxira") -and ($cfg.Log.Papka -eq "$data1\logs")) "zaxira va jurnal yo'llari"
    Check ($cfg.Jwt.Kalit -match '^[0-9a-f]{64}$') "JWT kaliti 64 hex: $($cfg.Jwt.Kalit.Substring(0, 8))..."
    Check ($cfg.Seed.AdminParol -ceq $pwd1) "AdminParol maxsus belgilar bilan to'g'ri yozilgan"
    $key1 = $cfg.Jwt.Kalit
    $loc = Json "$app1\Server\appsettings.Local.json"
    Check (($loc.Cors.Manbalar | Measure-Object).Count -eq 0) "Local.json: bo'sh Cors:Manbalar"
    Check ((Json "$app1\Desktop\sozlama.json").ServerManzili -eq "http://localhost:$p1") "desktop sozlama.json: http://localhost:$p1"
    Check ((Get-Content "$app1\FuelControl Web.url" -Raw) -match "URL=http://localhost:$p1") "Web yorlig'i: localhost:$p1"

    "== 3. Haqiqiy publish qilingan API Production rejimida (konsol), 127.0.0.1:$p1 =="
    $up = Start-Api $app1 $p1
    Check $up "API ishga tushdi va /openapi/v1.json 200 qaytardi"
    if ($up) {
        $root = Invoke-WebRequest "http://127.0.0.1:$p1/" -UseBasicParsing
        Check (($root.StatusCode -eq 200) -and ($root.Content -match '<app-root|<title')) "'/' PWA index.html (200, $($root.Headers['Content-Type']))"
        $mf = Invoke-WebRequest "http://127.0.0.1:$p1/manifest.webmanifest" -UseBasicParsing
        Check ($mf.Headers['Content-Type'] -match 'application/manifest\+json') "manifest MIME: $($mf.Headers['Content-Type'])"
        $r = Login $p1 $pwd1
        Check ($null -ne $r -and $r.foydalanuvchi.login -eq 'admin') "admin login'i o'rnatishdagi (maxsus belgili) parol bilan ishladi"
        Check ($null -eq (Login $p1 'notogri-parol')) "noto'g'ri parol rad etildi"
        # Toza o'rnatish (Production, baza yo'q): bazada faqat admin; demo, yoqilg'i, aparat, smena, nasiya yaratilmaydi.
        $api = "http://127.0.0.1:$p1"; $hdr = @{ Authorization = "Bearer $($r.token)" }
        $me = Invoke-RestMethod "$api/me" -Headers $hdr
        Check (($me.login -eq 'admin') -and ($me.rol -eq 'Admin')) "/me = admin (Admin)"
        Check (@(Invoke-RestMethod "$api/foydalanuvchilar" -Headers $hdr).Count -eq 1) "bazada bitta foydalanuvchi (admin)"
        foreach ($yol in '/yoqilgilar', '/aparatlar', '/smenalar') {
            Check ((Invoke-WebRequest "$api$yol" -Headers $hdr -UseBasicParsing).Content.Trim() -eq '[]') "toza baza: $yol bo'sh"
        }
        $nas = Invoke-RestMethod "$api/nasiyalar" -Headers $hdr
        Check ((@($nas.royxat).Count -eq 0) -and ($nas.xulosa.faolQarz -eq 0)) "toza baza: /nasiyalar bo'sh"
        Check ((Invoke-WebRequest "$api/smenalar/joriy" -Headers $hdr -UseBasicParsing).StatusCode -eq 204) "toza baza: ochiq smena yo'q (204)"
        $och = $null
        try { Invoke-RestMethod "$api/smenalar/och" -Method Post -Headers $hdr -ContentType 'application/json' -Body '{"qaytim":0,"terminal":0,"depozit":0}' | Out-Null } catch { $och = $_ }
        Check ($och -and ([int]$och.Exception.Response.StatusCode -eq 400) -and ($och.ErrorDetails.Message -match 'kamida bitta aparat')) "aparatsiz smena ochilmaydi: 400 'kamida bitta aparat kerak'"
        $doc = $null; try { $doc = Invoke-WebRequest "http://127.0.0.1:$p1/scalar/v1" -UseBasicParsing } catch { }
        Check ($null -ne $doc) "Scalar hujjat sahifasi ochiladi"
    }
    Stop-Api
    Check (Test-Path "$data1\fuelcontrol.db") "baza fayli ma'lumotlar papkasida yaratildi"
    $logFile = Get-ChildItem "$data1\logs\fuelcontrol-*.log" -ErrorAction SilentlyContinue | Select-Object -First 1
    Check ($null -ne $logFile) "jurnal fayli: $($logFile.Name)"
    if ($logFile) {
        $lt = Get-Content $logFile.FullName -Raw
        Check ($lt -match ('FuelControl API tayyor\. Versiya ' + [regex]::Escape($Version) + ', muhit Production')) "jurnalda 'tayyor', versiya $Version, muhit Production"
        Check ($lt -match [regex]::Escape("$app1\Server")) "kontent ildizi o'rnatilgan Server papkasi"
        Check ($lt -notmatch [regex]::Escape($pwd1) -and $lt -notmatch [regex]::Escape($key1)) "jurnalda parol ham, JWT kaliti ham yo'q"
    }

    "== 4. Yangilash: qayta o'rnatish (port $p2, parol berilmaydi), Local.json'ga qo'lda CORS qo'shilgan =="
    $origin = 'https://web-sinov.example'
    Set-Content -LiteralPath "$app1\Server\appsettings.Local.json" -Encoding utf8NoBOM -Value "{ `"Cors`": { `"Manbalar`": [ `"$origin`" ] } }"
    $code = Run-Setup @("/DIR=$app1", "/DATADIR=$data1", "/PORT=$p2", '/COMPONENTS=server,desktop', '/TASKS=') 'install2'
    Check ($code -eq 0) "yangilash chiqish kodi 0 (kod $code)"
    $cfg2 = Json "$app1\Server\appsettings.Production.json"
    Check ($cfg2.Jwt.Kalit -ceq $key1) "JWT kaliti ESKISI saqlandi"
    Check ($null -eq $cfg2.Seed) "AdminParol konfiguratsiyada yo'q (baza bor - parol so'ralmaydi)"
    Check ($cfg2.Urls -eq "http://0.0.0.0:$p2") "port yangilandi: $($cfg2.Urls)"
    Check ((Get-Content "$app1\Server\appsettings.Local.json" -Raw) -match [regex]::Escape($origin)) "Local.json (qo'lda tahrirlangan) saqlandi"
    Check ((Json "$app1\Desktop\sozlama.json").ServerManzili -eq "http://localhost:$p2") "desktop sozlama.json yangi portga"
    $up = Start-Api $app1 $p2
    Check $up "yangilangan API ishga tushdi"
    if ($up) {
        $r = Login $p2 $pwd1
        Check ($null -ne $r -and $r.foydalanuvchi.login -eq 'admin') "eski baza: admin eski parol bilan kiradi (ma'lumot saqlandi)"
        $req = [Net.HttpWebRequest]::Create("http://127.0.0.1:$p2/openapi/v1.json"); $req.Headers.Add('Origin', $origin)
        $resp = $req.GetResponse(); $acao = $resp.Headers['Access-Control-Allow-Origin']; $resp.Close()
        Check ($acao -eq $origin) "Local.json dagi CORS manbasi ishlaydi (Access-Control-Allow-Origin: $acao)"
    }
    Stop-Api

    "== 5. Faqat desktop: server manzili oxirida '/' bilan =="
    $app2 = "$T\app2"; $data2 = "$T\data2"
    $code = Run-Setup @("/DIR=$app2", "/DATADIR=$data2", "/SERVERURL=http://127.0.0.1:$p2/", '/COMPONENTS=desktop', '/TASKS=') 'install3'
    Check ($code -eq 0) "desktop-only chiqish kodi 0 (kod $code)"
    Check ((Test-Path "$app2\Desktop\FuelControl.exe") -and -not (Test-Path "$app2\Server")) "faqat Desktop o'rnatildi"
    Check ((Json "$app2\Desktop\sozlama.json").ServerManzili -eq "http://127.0.0.1:$p2") "sozlama.json: oxirgi / olib tashlandi"
    Check (-not (Test-Path $data2)) "ma'lumot papkasi yaratilmadi (server yo'q)"

    $app3 = "$T\app3"; $app4 = "$T\app4"
    if ($SkipTunnel) { "== 6. Tunnel komponenti: o'tkazib yuborildi (-SkipTunnel) ==" }
    else {
        "== 6. Tunnel komponenti: token bilan fayl o'rnatiladi, tokensiz o'rnatilmaydi =="
        $token = 'eyJhIjoiMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAiLCJ0IjoiMTIzNDU2NzgtMTIzNC0xMjM0LTEyMzQtMTIzNDU2Nzg5MDEyIn0='
        $code = Run-Setup @("/DIR=$app3", "/DATADIR=$T\data3", "/PORT=$p3", '/ADMINPAROL=KuchliParol1', '/COMPONENTS=server,tunnel', "/TUNNELTOKEN=$token") 'install4'
        Check (($code -eq 0) -and (Test-Path "$app3\Tunnel\cloudflared.exe")) "token bilan: cloudflared.exe o'rnatildi (kod $code)"
        $code = Run-Setup @("/DIR=$app4", "/DATADIR=$T\data4", "/PORT=$p4", '/ADMINPAROL=KuchliParol1', '/COMPONENTS=server,tunnel') 'install5'
        Check (($code -eq 0) -and -not (Test-Path "$app4\Tunnel")) "tokensiz: Tunnel o'tkazib yuborildi (kod $code)"
        # Inno buyruq satrini jurnalning boshiga o'zi yozadi (docs\ornatish.md, 13-bo'lim); o'rnatuvchi kodi tokenni yozmasligi kerak.
        $leak = @(Get-Content "$T\install4.log" | Where-Object { $_ -match [regex]::Escape($token) -and $_ -notmatch 'Setup command line:' }).Count
        Check ($leak -eq 0) "tunnel tokeni jurnalda faqat Inno'ning 'Setup command line' sarlavhasida (o'rnatuvchi kodi yozmaydi)"
    }

    "== 7. O'chirish: dastur fayllari ketadi, ma'lumotlar qoladi =="
    Uninstall-All
    Check (-not (Test-Path "$app1\Server\appsettings.Production.json") -and -not (Test-Path "$app1\Server\appsettings.Local.json") -and -not (Test-Path "$app1\Desktop\sozlama.json")) "o'rnatuvchi yozgan sozlama fayllari o'chdi"
    Check (-not (Test-Path "$app1\Server\FuelControl.Api.exe") -and -not (Test-Path "$app1\Desktop\FuelControl.exe")) "dastur fayllari o'chdi"
    Check (-not (Test-Path "$app1\unins000.exe")) "uninstaller o'zini o'chirdi"
    Check ((Test-Path "$data1\fuelcontrol.db") -and (Test-Path "$data1\logs") -and (Test-Path "$data1\zaxira")) "ma'lumotlar SAQLANDI (baza, jurnal, zaxira)"
    Check (-not (Test-Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$appId}_is1")) "ro'yxatdan o'tish yozuvi (HKCU) o'chdi"
}
catch { "!!! ISTISNO: $($_.Exception.Message)"; $_.InvocationInfo.PositionMessage; $_.ScriptStackTrace; $script:fail++ }
finally {
    Stop-Api
    Uninstall-All
}

""
if ($script:fail -eq 0) {
    'HAMMASI O''TDI'
    if (-not $KeepFiles) { try { [IO.Directory]::Delete($T, $true) } catch { Write-Warning "Ish papkasini o'chirib bo'lmadi: $T" } }
}
else {
    "$($script:fail) TA XATO (jurnallar: $T)"
    exit 1
}
