; ============================================================================================================
; FuelControl o'rnatuvchisi (Inno Setup 7; 6.x da ham kompilyatsiya bo'ladi).
; Odatda build.ps1 orqali yig'iladi: web build -> API/Desktop publish -> (cloudflared) -> ISCC.
; To'g'ridan-to'g'ri kompilyatsiya uchun build.ps1 ni bir marta ishga tushiring (build\stage to'ladi).
;
; Komponentlar:  server  = API + web, Windows xizmati "FuelControl", self-contained win-x64
;                desktop = Avalonia dastur, self-contained win-x64
;                tunnel  = ixtiyoriy Cloudflare Tunnel (cloudflared xizmati; token wizard'da)
; Ma'lumotlar:   %ProgramData%\FuelControl\  (fuelcontrol.db, zaxira\, logs\) — o'chirishda SAQLANADI.
;
; Jim o'rnatish parametrlari (standart Inno parametrlariga qo'shimcha):
;   /PORT=5000  /ADMINPAROL=...  /SERVERURL=http://server:5000  /TUNNELTOKEN=...  /DATADIR=D:\FuelControlData
;   /NOSYSTEM=1  — faqat fayllar va sozlama fayllari (xizmat, xavfsizlik devori, ruxsatlar, cloudflared o'tkazib
;                  yuboriladi): administratorsiz sinash uchun, /CURRENTUSER bilan birga.
; ============================================================================================================

#define AppName      "FuelControl"
#define AppPublisher "Ovoza dasturlar"

; Yo'llar: build.ps1 beradi; bo'lmasa skript papkasiga nisbatan.
#ifndef SourceRoot
  #define SourceRoot AddBackslash(SourcePath) + "..\.."
#endif
#ifndef ServerDir
  #define ServerDir AddBackslash(SourcePath) + "build\stage\server"
#endif
#ifndef DesktopDir
  #define DesktopDir AddBackslash(SourcePath) + "build\stage\desktop"
#endif

; Versiya yagona manbadan: repo ildizidagi Directory.Build.props (<Version>). build.ps1 /DAppVersion beradi;
; IDE'dan to'g'ridan-to'g'ri kompilyatsiyada shu fayldan o'qiladi.
#ifndef AppVersion
  #define PropsFile AddBackslash(SourcePath) + "..\..\Directory.Build.props"
  #define PropsHandle FileOpen(PropsFile)
  #define PropsLine ""
  #define AppVersion ""
  #sub ReadPropsLine
    #expr PropsLine = FileRead(PropsHandle)
    #if Pos("<Version>", PropsLine) > 0
      #expr AppVersion = Copy(PropsLine, Pos("<Version>", PropsLine) + 9, Pos("</Version>", PropsLine) - Pos("<Version>", PropsLine) - 9)
    #endif
  #endsub
  #for {0; AppVersion == "" && !FileEof(PropsHandle); 0} ReadPropsLine
  #expr FileClose(PropsHandle)
  #if AppVersion == ""
    #error Directory.Build.props ichida <Version> topilmadi
  #endif
#endif

[Setup]
; AppId — doimiy (yangilash va o'chirish shu bilan topiladi): HECH QACHON o'zgartirmang.
AppId={{08499963-05FB-4806-9A64-0EBC3A1CE9D5}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}.0
VersionInfoProductVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} o'rnatuvchisi
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=Output
OutputBaseFilename=FuelControl-Setup-{#AppVersion}
SetupIconFile={#SourceRoot}\src\frontend\desktop\FuelControl.Desktop\Assets\fuelcontrol.ico
UninstallDisplayIcon={app}\fuelcontrol.ico
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.14393
; Server komponenti Windows xizmati, xavfsizlik devori qoidasi va ProgramData bilan ishlaydi — administrator kerak.
; /CURRENTUSER faqat administratorsiz sinash uchun (/NOSYSTEM=1 bilan); dialog ko'rsatilmaydi.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
; Ochiq desktop yopilishini so'raydi; API xizmatini o'zimiz to'xtatamiz (kodda), shuning uchun faqat desktop exe filtri.
CloseApplications=yes
CloseApplicationsFilter=FuelControl.exe
RestartApplications=no
ShowLanguageDialog=no
LanguageDetectionMethod=none

[Languages]
; Uzbek.isl — Inno Setup "Unofficial" tarjimalaridan (Shamsiddinov Zafar), o'zgartirilmagan nusxa; o'zimizning matnlar
; [CustomMessages] da. Yo'q xabarlar inglizcha standartga tushadi.
Name: "uz"; MessagesFile: "Languages\Uzbek.isl"

[Types]
Name: "full";        Description: "{cm:TypeFull}"
Name: "desktoponly"; Description: "{cm:TypeDesktop}"
Name: "custom";      Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "server";  Description: "{cm:CompServer}";  Types: full
Name: "desktop"; Description: "{cm:CompDesktop}"; Types: full desktoponly
#ifdef TunnelDir
; Hech bir tipga kirmaydi: standart holda belgilanmagan (ixtiyoriy).
Name: "tunnel";  Description: "{cm:CompTunnel}"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktopIcon}"; Components: desktop

[Dirs]
; Ma'lumotlar papkasi o'chirishda saqlanadi.
Name: "{code:DataDir}";        Components: server; Flags: uninsneveruninstall
Name: "{code:DataDir}\zaxira"; Components: server; Flags: uninsneveruninstall
Name: "{code:DataDir}\logs";   Components: server; Flags: uninsneveruninstall

[InstallDelete]
; Yangilashda eski web fayllari (hash'li nomlar) va eski dll'lar to'planib qolmasin. Sozlama fayllariga tegilmaydi.
Type: filesandordirs; Name: "{app}\Server\wwwroot"; Components: server
Type: files;          Name: "{app}\Server\*.dll";   Components: server
Type: files;          Name: "{app}\Desktop\*.dll";  Components: desktop

[Files]
Source: "{#SourceRoot}\src\frontend\desktop\FuelControl.Desktop\Assets\fuelcontrol.ico"; DestDir: "{app}"; Flags: ignoreversion
; Dev konfiguratsiyalar (kalit, parol) va qo'lda tahrirlanadigan fayllar hech qachon o'rnatuvchiga kirmaydi.
Source: "{#ServerDir}\*";  DestDir: "{app}\Server";  Components: server;  Excludes: "appsettings.Development.json,appsettings.Web.json,appsettings.Production.json,appsettings.Local.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#DesktopDir}\*"; DestDir: "{app}\Desktop"; Components: desktop; Excludes: "sozlama.json"; Flags: ignoreversion recursesubdirs createallsubdirs
#ifdef TunnelDir
Source: "{#TunnelDir}\cloudflared.exe"; DestDir: "{app}\Tunnel"; Components: tunnel; Check: TunnelConfigured; Flags: ignoreversion
#endif

[INI]
Filename: "{app}\FuelControl Web.url"; Section: "InternetShortcut"; Key: "URL"; String: "http://localhost:{code:PortText}"; Components: server

[Icons]
Name: "{autoprograms}\{#AppName}";     Filename: "{app}\Desktop\FuelControl.exe"; Components: desktop
Name: "{autodesktop}\{#AppName}";      Filename: "{app}\Desktop\FuelControl.exe"; Components: desktop; Tasks: desktopicon
Name: "{autoprograms}\{cm:ShortcutWeb}"; Filename: "{app}\FuelControl Web.url"; IconFilename: "{app}\fuelcontrol.ico"; Components: server

[Run]
; Desktop'ni o'rnatuvchi (administrator) emas, asl foydalanuvchi ochadi: sozlamalar o'sha foydalanuvchining %AppData% ida.
Filename: "{app}\Desktop\FuelControl.exe"; Description: "{cm:LaunchDesktop}"; Flags: nowait postinstall skipifsilent runasoriginaluser; Components: desktop

[UninstallDelete]
; Kod yozgan fayllar. Baza, zaxira va jurnallar ({commonappdata}\FuelControl) ataylab O'CHIRILMAYDI.
Type: files; Name: "{app}\Server\appsettings.Production.json"
Type: files; Name: "{app}\Server\appsettings.Local.json"
Type: files; Name: "{app}\Desktop\sozlama.json"
Type: files; Name: "{app}\FuelControl Web.url"
Type: files; Name: "{app}\nosystem.flag"

[CustomMessages]
uz.TypeFull=To'liq (server + desktop)
uz.TypeDesktop=Faqat desktop (boshqa kompyuter)
uz.TypeCustom=Maxsus
uz.CompServer=Server: API + web (Windows xizmati)
uz.CompDesktop=Desktop dastur
uz.CompTunnel=Cloudflare Tunnel (ixtiyoriy)
uz.TaskDesktopIcon=Ish stolida yorliq yaratish
uz.LaunchDesktop=FuelControl'ni ishga tushirish
uz.ShortcutWeb=FuelControl Web

uz.ComponentsNeedOne=Kamida bitta komponentni tanlang: Server yoki Desktop.
uz.TunnelNeedsServer=Cloudflare Tunnel faqat Server bilan birga o'rnatiladi. Server komponentini ham tanlang yoki Tunnel'ni olib tashlang.
uz.NeedAdmin=Server komponenti Windows xizmati sifatida o'rnatiladi va administrator huquqini talab qiladi. O'rnatuvchini "Administrator sifatida ishga tushirish" bilan qayta oching.

uz.ServerPageTitle=Server sozlamalari
uz.ServerPageDescr=FuelControl serveri uchun port va administrator paroli.
uz.ServerPageSubNew=Server shu portda tarmoqqa ochiladi. Administrator (login: admin) paroli birinchi kirish uchun kerak.
uz.ServerPageSubUpdate=Mavjud baza topildi: administrator paroli qayta so'ralmaydi, barcha ma'lumotlar saqlanadi.
uz.PortLabel=Server porti (TCP):
uz.AdminPwdLabel=Administrator paroli (kamida 8 belgi):
uz.AdminPwd2Label=Parolni tasdiqlang:
uz.PortInvalid=Port 1 dan 65535 gacha son bo'lishi kerak.
uz.PortBusy=%1-port band (boshqa dastur eshitmoqda). Boshqa port tanlang.
uz.PwdShort=Administrator paroli kamida 8 belgidan iborat bo'lishi kerak.
uz.PwdMismatch=Parol va tasdiqlash bir xil emas.

uz.RemotePageTitle=Server manzili
uz.RemotePageDescr=Desktop dastur qaysi serverga ulanishini ko'rsating.
uz.RemotePageSub=Server o'rnatilgan kompyuter manzilini kiriting, masalan: http://192.168.1.10:5000
uz.ServerUrlLabel=Server manzili:
uz.ServerUrlInvalid=Manzil http:// yoki https:// bilan boshlanishi va bo'sh joysiz bo'lishi kerak.
uz.ServerUrlUnreachable=%1 manzilidagi server javob bermadi. Baribir davom etasizmi?

uz.TunnelPageTitle=Cloudflare Tunnel
uz.TunnelPageDescr=Serverni internetdan Cloudflare orqali ko'rinadigan qilish.
uz.TunnelPageSub=Cloudflare Zero Trust panelidan (Networks > Tunnels) tunnel tokenini nusxalab qo'ying. Bo'sh qoldirsangiz, Cloudflare Tunnel o'rnatilmaydi.
uz.TunnelTokenLabel=Tunnel tokeni:
uz.TunnelTokenInvalid=Token noto'g'ri ko'rinadi. Cloudflare panelidagi to'liq tokenni nusxalab qo'ying.

uz.KeyFailed=Maxfiy kalitni yaratib bo'lmadi (Windows tasodifiy son generatori javob bermadi). O'rnatish to'xtatildi.

uz.StatusStopService=Eski xizmat to'xtatilmoqda...
uz.StatusConfig=Sozlamalar yozilmoqda...
uz.StatusService=Windows xizmati sozlanmoqda...
uz.StatusFirewall=Xavfsizlik devori qoidasi qo'shilmoqda...
uz.StatusStart=FuelControl xizmati ishga tushirilmoqda...
uz.StatusTunnel=Cloudflare Tunnel o'rnatilmoqda...

uz.WarnHeader=O'rnatish tugadi, lekin quyidagilarni tekshiring:
uz.WarnServiceStart=FuelControl xizmati ishga tushmadi (kod %1). Sababi jurnalda: %2
uz.WarnApiNoAnswer=Xizmat ishga tushdi, lekin %1-portda javob bermadi. Jurnalni tekshiring: %2
uz.WarnFirewall=Xavfsizlik devori qoidasini qo'shib bo'lmadi (kod %1). %2-portni qo'lda oching.
uz.WarnAcl=Maxfiy fayllarga kirishni cheklab bo'lmadi (kod %1).
uz.WarnServiceCreate=Windows xizmatini yaratib bo'lmadi (kod %1).
uz.WarnTunnelExists=Cloudflared xizmati allaqachon boshqa joydan o'rnatilgan - tegilmadi.
uz.WarnTunnelFail=Cloudflare Tunnel o'rnatilmadi (kod %1). Tokenni tekshiring.
uz.FinishedWeb=Veb-sahifa: http://localhost:%1  (tarmoqda: http://%2:%1)
uz.UninstalledKeepData=FuelControl o'chirildi. Baza, zaxira nusxalar va jurnallar saqlandi: %1

[Code]
const
  ServiceName = 'FuelControl';
  CloudflaredService = 'Cloudflared';
  FirewallRuleName = 'FuelControl API';
  DefaultPort = 5000;

var
  ServerPage: TInputQueryWizardPage;
  RemotePage: TInputQueryWizardPage;
  TunnelPage: TInputQueryWizardPage;
  JwtKey: String;          // yangi yoki oldingi o'rnatishdan saqlangan JWT kaliti
  Warnings: String;        // o'rnatish oxirida ko'rsatiladigan ogohlantirishlar

// Windows tasodifiy son generatori (kriptografik); Inno'ning Random()'i JWT kaliti uchun yaroqsiz.
function RtlGenRandom(var Buffer: Cardinal; BufLen: Cardinal): Boolean; external 'SystemFunction036@advapi32.dll stdcall';

{ ---------- umumiy yordamchilar ---------- }

// /NOMI=qiymat ko'rinishidagi parametr (nomi katta-kichik harfga bog'liq emas). ParamStr to'g'ridan-to'g'ri o'qiladi:
// qiymatdagi } | , % kabi belgilar (parollarda uchraydi) {param:...} konstantasidagidek muammo tug'dirmaydi.
function CmdParam(const Name, Default: String): String;
var
  I: Integer;
  P, Prefix: String;
begin
  Result := Default;
  Prefix := '/' + Uppercase(Name) + '=';
  for I := 1 to ParamCount do
  begin
    P := ParamStr(I);
    if Pos(Prefix, Uppercase(P)) = 1 then
    begin
      P := Copy(P, Length(Prefix) + 1, Length(P));
      if P <> '' then Result := P;
      Exit;
    end;
  end;
end;

function NoSystem: Boolean;
begin
  Result := CmdParam('NOSYSTEM', '0') = '1';
end;

// Ma'lumotlar papkasi: standart %ProgramData%\FuelControl, /DATADIR=... bilan o'zgartiriladi.
function DataDir(Param: String): String;
var
  S: String;
begin
  S := CmdParam('DATADIR', '');
  if S = '' then S := ExpandConstant('{commonappdata}\FuelControl');
  Result := RemoveBackslash(S);
end;

function WebAppDir: String;
begin
  // Wizard ichida {app} hali tanlanmagan bo'lishi mumkin — Select Dir sahifasidagi qiymat (yangilashda oldingi papka).
  if WizardForm <> nil then Result := RemoveBackslash(WizardDirValue)
  else Result := RemoveBackslash(ExpandConstant('{app}'));
end;

function ServerConfigPath: String;
begin
  Result := WebAppDir + '\Server\appsettings.Production.json';
end;

function ReadTextFile(const FileName: String): String;
var
  Raw: AnsiString;
begin
  Result := '';
  if LoadStringFromFile(FileName, Raw) then Result := String(Raw);
end;

// Oddiy JSON satr qiymatini ajratadi: "Key": "qiymat" (qochirilgan belgilarsiz qiymatlar uchun — kalit, URL).
function JsonStringValue(const Text, Key: String): String;
var
  P, Q: Integer;
  Rest: String;
begin
  Result := '';
  P := Pos('"' + Key + '"', Text);
  if P = 0 then Exit;
  Rest := Copy(Text, P + Length(Key) + 2, Length(Text));
  P := Pos(':', Rest);
  if P = 0 then Exit;
  Rest := Copy(Rest, P + 1, Length(Rest));
  P := Pos('"', Rest);
  if P = 0 then Exit;
  Rest := Copy(Rest, P + 1, Length(Rest));
  Q := Pos('"', Rest);
  if Q = 0 then Exit;
  Result := Copy(Rest, 1, Q - 1);
end;

function JsonEscape(const S: String): String;
var
  I, Code: Integer;
  C: Char;
begin
  Result := '';
  for I := 1 to Length(S) do
  begin
    C := S[I];
    Code := Ord(C);
    if C = '\' then Result := Result + '\\'
    else if C = '"' then Result := Result + '\"'
    else if Code < 32 then
      Result := Result + '\u00' + Copy('0123456789abcdef', (Code div 16) + 1, 1) + Copy('0123456789abcdef', (Code mod 16) + 1, 1)
    else Result := Result + C;
  end;
end;

function IsHexString(const S: String): Boolean;
var
  I: Integer;
  C: Char;
begin
  Result := Length(S) > 0;
  for I := 1 to Length(S) do
  begin
    C := S[I];
    if not (((C >= '0') and (C <= '9')) or ((C >= 'a') and (C <= 'f')) or ((C >= 'A') and (C <= 'F'))) then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function Hex8(V: Cardinal): String;
var
  I, D: Integer;
begin
  Result := '';
  for I := 1 to 8 do
  begin
    D := Integer(V mod 16);
    V := V div 16;
    Result := Copy('0123456789abcdef', D + 1, 1) + Result;
  end;
end;

// Words * 8 ta o'n oltilik belgi. Muvaffaqiyatsiz bo'lsa — bo'sh satr (zaif zaxira kalit YO'Q).
function RandomHex(Words: Integer): String;
var
  I: Integer;
  V: Cardinal;
begin
  Result := '';
  for I := 1 to Words do
  begin
    V := 0;
    if not RtlGenRandom(V, 4) then
    begin
      Result := '';
      Exit;
    end;
    Result := Result + Hex8(V);
  end;
end;

function PortFromUrls(const Urls: String): Integer;
var
  I: Integer;
  Digits: String;
begin
  Result := 0;
  Digits := '';
  I := Length(Urls);
  while (I >= 1) and (Copy(Urls, I, 1) >= '0') and (Copy(Urls, I, 1) <= '9') do
  begin
    Digits := Copy(Urls, I, 1) + Digits;
    I := I - 1;
  end;
  if (I >= 1) and (Copy(Urls, I, 1) = ':') then Result := StrToIntDef(Digits, 0);
end;

function LastWord(const S: String): String;
var
  I: Integer;
begin
  Result := Trim(S);
  for I := Length(Result) downto 1 do
    if Copy(Result, I, 1) = ' ' then
    begin
      Result := Copy(Result, I + 1, Length(Result));
      Exit;
    end;
end;

procedure AddWarning(const Msg: String);
begin
  Log('OGOHLANTIRISH: ' + Msg);
  Warnings := Warnings + #13#10 + ' - ' + Msg;
end;

{ ---------- sahifa qiymatlari ---------- }

function ServerPort: Integer;
begin
  if ServerPage <> nil then Result := StrToIntDef(Trim(ServerPage.Values[0]), 0)
  else Result := DefaultPort;
end;

function PortText(Param: String): String;
begin
  Result := IntToStr(ServerPort);
end;

function NeedAdminPassword: Boolean;
begin
  // Baza hali yo'q bo'lsa API birinchi ishga tushishda admin foydalanuvchini yaratadi — parol kerak.
  Result := not FileExists(DataDir('') + '\fuelcontrol.db');
end;

function ServerUrl: String;
var
  S: String;
begin
  S := Trim(RemotePage.Values[0]);
  while (Length(S) > 0) and (Copy(S, Length(S), 1) = '/') do S := Copy(S, 1, Length(S) - 1);
  Result := S;
end;

function TunnelToken: String;
begin
  if TunnelPage = nil then Result := ''
  else Result := LastWord(TunnelPage.Values[0]);   // butun "cloudflared service install <token>" qo'yilsa ham oxirgi so'z olinadi
end;

function IsTunnelSelected: Boolean;
begin
#ifdef TunnelDir
  Result := WizardIsComponentSelected('tunnel');
#else
  Result := False;
#endif
end;

function TunnelConfigured: Boolean;
begin
  Result := IsTunnelSelected and (TunnelToken <> '');
end;

{ ---------- Windows xizmatlari va tarmoq ---------- }

function RunCmd(const Exe, Params: String; var ResultCode: Integer): Boolean;
var
  Shown: String;
begin
  Result := Exec(Exe, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Shown := Params;
  if Pos('service install ', Params) = 1 then Shown := 'service install <token>';   // maxfiy token jurnalga tushmasin
  Log(Exe + ' ' + Shown + '  ->  ' + IntToStr(ResultCode));
end;

function ServiceExists(const Name: String): Boolean;
var
  RC: Integer;
begin
  Result := RunCmd(ExpandConstant('{sys}\sc.exe'), 'query "' + Name + '"', RC) and (RC = 0);
end;

function ServiceImagePath(const Name: String): String;
begin
  if not RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Services\' + Name, 'ImagePath', Result) then Result := '';
end;

// Xizmat aynan shu o'rnatish papkasidagi exe'ga ishora qiladimi (boshqa nusxaga tegmaslik uchun).
function ServiceIsOurs(const Name, Folder: String): Boolean;
begin
  Result := Pos(Lowercase(Folder), Lowercase(ServiceImagePath(Name))) > 0;
end;

procedure StopService(const Name: String);
var
  RC: Integer;
begin
  // "net stop" xizmat to'xtaguncha kutadi (ishlamayotgan bo'lsa 2 qaytaradi — zararsiz).
  RunCmd(ExpandConstant('{sys}\net.exe'), 'stop "' + Name + '"', RC);
end;

function PortInUse(Port: Integer): Boolean;
var
  RC: Integer;
  Tmp: String;
begin
  Result := False;
  Tmp := ExpandConstant('{tmp}\fc-port.txt');
  DeleteFile(Tmp);
  if RunCmd(ExpandConstant('{cmd}'), '/C netstat -ano -p tcp | findstr /R /C:":' + IntToStr(Port) + ' .*LISTENING" > "' + Tmp + '"', RC) then
    Result := Length(Trim(ReadTextFile(Tmp))) > 0;
  DeleteFile(Tmp);
end;

// GET url -> 200 bo'lsa True.
function HttpOk(const Url: String; TimeoutMs: Integer): Boolean;
var
  Req: Variant;
begin
  Result := False;
  try
    Req := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    Req.Open('GET', Url, False);
    Req.SetTimeouts(TimeoutMs, TimeoutMs, TimeoutMs, TimeoutMs);
    Req.Send('');
    Result := Req.Status = 200;
  except
    Result := False;
  end;
end;

function WaitForApi(Port, Seconds: Integer): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to Seconds do
  begin
    if HttpOk('http://127.0.0.1:' + IntToStr(Port) + '/openapi/v1.json', 2000) then
    begin
      Result := True;
      Exit;
    end;
    Sleep(1000);
  end;
end;

{ ---------- sozlama fayllari ---------- }

procedure AddLine(var Lines: TArrayOfString; const S: String);
var
  N: Integer;
begin
  N := GetArrayLength(Lines);
  SetArrayLength(Lines, N + 1);
  Lines[N] := S;
end;

// appsettings.Production.json — o'rnatuvchiniki: har o'rnatishda qayta yoziladi (kalit saqlanadi).
// Qo'lda o'zgartirish uchun appsettings.Local.json ishlatiladi (unga tegilmaydi).
procedure WriteServerConfig(const AdminPwd: String);
var
  Lines: TArrayOfString;
  Data: String;
begin
  SetArrayLength(Lines, 0);
  Data := DataDir('');
  AddLine(Lines, '// Bu fayl FuelControl o''rnatuvchisi tomonidan yaratiladi va har o''rnatishda qayta yoziladi.');
  AddLine(Lines, '// O''zgartirishlar uchun appsettings.Local.json dan foydalaning (o''rnatuvchi unga tegmaydi).');
  AddLine(Lines, '{');
  AddLine(Lines, '  "Urls": "http://0.0.0.0:' + IntToStr(ServerPort) + '",');
  AddLine(Lines, '  "ConnectionStrings": { "Baza": "Data Source=' + JsonEscape(Data + '\fuelcontrol.db') + '" },');
  AddLine(Lines, '  "Zaxira": { "Papka": "' + JsonEscape(Data + '\zaxira') + '" },');
  AddLine(Lines, '  "Log": { "Papka": "' + JsonEscape(Data + '\logs') + '" },');
  if AdminPwd <> '' then
  begin
    AddLine(Lines, '  "Jwt": { "Kalit": "' + JwtKey + '" },');
    AddLine(Lines, '  "Seed": { "AdminParol": "' + JsonEscape(AdminPwd) + '" }');
  end
  else
    AddLine(Lines, '  "Jwt": { "Kalit": "' + JwtKey + '" }');
  AddLine(Lines, '}');
  SaveStringsToUTF8FileWithoutBOM(ExpandConstant('{app}\Server\appsettings.Production.json'), Lines, False);
end;

procedure WriteLocalConfigIfMissing;
var
  Lines: TArrayOfString;
  Path: String;
begin
  Path := ExpandConstant('{app}\Server\appsettings.Local.json');
  if FileExists(Path) then Exit;
  SetArrayLength(Lines, 0);
  AddLine(Lines, '// Administrator tahrirlaydigan sozlamalar (o''rnatuvchi bu faylga tegmaydi). O''zgartirgach "FuelControl" xizmatini qayta ishga tushiring.');
  AddLine(Lines, '// Web alohida domenda (masalan Cloudflare Pages) bo''lsa, uning manzilini Manbalar ro''yxatiga qo''shing, masalan:');
  AddLine(Lines, '//   "Cors": { "Manbalar": [ "https://fuelcontrol.pages.dev" ] }');
  AddLine(Lines, '{');
  AddLine(Lines, '  "Cors": { "Manbalar": [] }');
  AddLine(Lines, '}');
  SaveStringsToUTF8FileWithoutBOM(Path, Lines, False);
end;

procedure WriteDesktopSettings;
var
  Lines: TArrayOfString;
  Url: String;
begin
  if WizardIsComponentSelected('server') then Url := 'http://localhost:' + IntToStr(ServerPort)
  else Url := ServerUrl;
  SetArrayLength(Lines, 3);
  Lines[0] := '{';
  Lines[1] := '  "ServerManzili": "' + JsonEscape(Url) + '"';
  Lines[2] := '}';
  SaveStringsToUTF8FileWithoutBOM(ExpandConstant('{app}\Desktop\sozlama.json'), Lines, False);
end;

{ ---------- tekshiruvlar (NextButtonClick va jim o'rnatishda ham ishlaydi) ---------- }

function CheckComponents: Boolean;
begin
  Result := True;
  if not (WizardIsComponentSelected('server') or WizardIsComponentSelected('desktop')) then
  begin
    SuppressibleMsgBox(CustomMessage('ComponentsNeedOne'), mbError, MB_OK, IDOK);
    Result := False;
  end
  else if IsTunnelSelected and not WizardIsComponentSelected('server') then
  begin
    SuppressibleMsgBox(CustomMessage('TunnelNeedsServer'), mbError, MB_OK, IDOK);
    Result := False;
  end
  else if WizardIsComponentSelected('server') and not IsAdmin and not NoSystem then
  begin
    SuppressibleMsgBox(CustomMessage('NeedAdmin'), mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function CheckServerPage: Boolean;
var
  Port: Integer;
  Pwd: String;
begin
  Result := False;
  Port := ServerPort;
  if (Port < 1) or (Port > 65535) then
  begin
    SuppressibleMsgBox(CustomMessage('PortInvalid'), mbError, MB_OK, IDOK);
    Exit;
  end;
  // Yangilashda o'zimizning xizmat shu portda eshitib turadi — bu xato emas.
  if not NoSystem then
    if not (ServiceExists(ServiceName) and (Port = PortFromUrls(JsonStringValue(ReadTextFile(ServerConfigPath), 'Urls')))) then
      if PortInUse(Port) then
      begin
        SuppressibleMsgBox(FmtMessage(CustomMessage('PortBusy'), [IntToStr(Port)]), mbError, MB_OK, IDOK);
        Exit;
      end;
  if NeedAdminPassword then
  begin
    Pwd := ServerPage.Values[1];
    if Length(Pwd) < 8 then
    begin
      SuppressibleMsgBox(CustomMessage('PwdShort'), mbError, MB_OK, IDOK);
      Exit;
    end;
    if Pwd <> ServerPage.Values[2] then
    begin
      SuppressibleMsgBox(CustomMessage('PwdMismatch'), mbError, MB_OK, IDOK);
      Exit;
    end;
  end;
  Result := True;
end;

function CheckRemotePage: Boolean;
var
  Url: String;
begin
  Result := False;
  Url := ServerUrl;
  if not (((Pos('http://', Lowercase(Url)) = 1) and (Length(Url) > 7)) or ((Pos('https://', Lowercase(Url)) = 1) and (Length(Url) > 8)))
     or (Pos(' ', Url) > 0) or (Pos('"', Url) > 0) then
  begin
    SuppressibleMsgBox(CustomMessage('ServerUrlInvalid'), mbError, MB_OK, IDOK);
    Exit;
  end;
  // Server hali o'rnatilmagan bo'lishi mumkin — javob bermasa so'raymiz, majburlamaymiz.
  if not HttpOk(Url + '/openapi/v1.json', 3000) then
    if SuppressibleMsgBox(FmtMessage(CustomMessage('ServerUrlUnreachable'), [Url]), mbConfirmation, MB_YESNO, IDYES) <> IDYES then
      Exit;
  Result := True;
end;

function CheckTunnelPage: Boolean;
var
  Token: String;
  I: Integer;
  C: Char;
begin
  Result := True;
  Token := TunnelToken;
  if Token = '' then Exit;          // bo'sh token — komponent o'tkazib yuboriladi
  Result := Length(Token) >= 40;
  for I := 1 to Length(Token) do
  begin
    C := Token[I];
    if not (((C >= '0') and (C <= '9')) or ((C >= 'a') and (C <= 'z')) or ((C >= 'A') and (C <= 'Z')) or (C = '-') or (C = '_') or (C = '=') or (C = '+') or (C = '/') or (C = '.')) then
      Result := False;
  end;
  if not Result then SuppressibleMsgBox(CustomMessage('TunnelTokenInvalid'), mbError, MB_OK, IDOK);
end;

{ ---------- Inno hodisalari ---------- }

procedure InitializeWizard;
var
  Existing: String;
  Port: Integer;
begin
  Existing := ReadTextFile(ServerConfigPath);
  Port := PortFromUrls(JsonStringValue(Existing, 'Urls'));
  if Port = 0 then Port := DefaultPort;

  ServerPage := CreateInputQueryPage(wpSelectComponents, CustomMessage('ServerPageTitle'), CustomMessage('ServerPageDescr'), '');
  ServerPage.Add(CustomMessage('PortLabel'), False);
  ServerPage.Add(CustomMessage('AdminPwdLabel'), True);
  ServerPage.Add(CustomMessage('AdminPwd2Label'), True);
  ServerPage.Values[0] := CmdParam('PORT', IntToStr(Port));
  ServerPage.Values[1] := CmdParam('ADMINPAROL', '');
  ServerPage.Values[2] := ServerPage.Values[1];

  RemotePage := CreateInputQueryPage(ServerPage.ID, CustomMessage('RemotePageTitle'), CustomMessage('RemotePageDescr'), CustomMessage('RemotePageSub'));
  RemotePage.Add(CustomMessage('ServerUrlLabel'), False);
  Existing := JsonStringValue(ReadTextFile(WebAppDir + '\Desktop\sozlama.json'), 'ServerManzili');
  if Existing = '' then Existing := 'http://';
  RemotePage.Values[0] := CmdParam('SERVERURL', Existing);

  TunnelPage := CreateInputQueryPage(RemotePage.ID, CustomMessage('TunnelPageTitle'), CustomMessage('TunnelPageDescr'), CustomMessage('TunnelPageSub'));
  TunnelPage.Add(CustomMessage('TunnelTokenLabel'), False);
  TunnelPage.Values[0] := CmdParam('TUNNELTOKEN', '');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = ServerPage.ID then Result := not WizardIsComponentSelected('server')
  else if PageID = RemotePage.ID then Result := WizardIsComponentSelected('server') or not WizardIsComponentSelected('desktop')
  else if PageID = TunnelPage.ID then Result := not IsTunnelSelected;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Need: Boolean;
begin
  if CurPageID = ServerPage.ID then
  begin
    Need := NeedAdminPassword;
    ServerPage.PromptLabels[1].Visible := Need;
    ServerPage.Edits[1].Visible := Need;
    ServerPage.PromptLabels[2].Visible := Need;
    ServerPage.Edits[2].Visible := Need;
    if Need then ServerPage.SubCaptionLabel.Caption := CustomMessage('ServerPageSubNew')
    else ServerPage.SubCaptionLabel.Caption := CustomMessage('ServerPageSubUpdate');
  end
  else if CurPageID = wpFinished then
  begin
    if WizardIsComponentSelected('server') then
      WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 +
        FmtMessage(CustomMessage('FinishedWeb'), [IntToStr(ServerPort), ExpandConstant('{computername}')]);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectComponents then Result := CheckComponents
  else if CurPageID = ServerPage.ID then Result := CheckServerPage
  else if CurPageID = RemotePage.ID then Result := CheckRemotePage
  else if CurPageID = TunnelPage.ID then Result := CheckTunnelPage;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Previous: String;
begin
  Result := '';
  Warnings := '';

  if WizardIsComponentSelected('server') then
  begin
    // JWT kaliti: yangilashda ESKISI saqlanadi (aks holda ochiq sessiyalar bekor bo'ladi); birinchi o'rnatishda yangi.
    Previous := JsonStringValue(ReadTextFile(ServerConfigPath), 'Kalit');
    if IsHexString(Previous) and (Length(Previous) >= 32) then JwtKey := Previous
    else JwtKey := RandomHex(8);   // 64 ta o'n oltilik belgi = 256 bit
    if JwtKey = '' then
    begin
      Result := CustomMessage('KeyFailed');
      Exit;
    end;
  end;

  if not NoSystem then
  begin
    // Yangilash: fayllar almashishidan oldin xizmatlarni to'xtatamiz (exe qulfini yechish uchun).
    if WizardIsComponentSelected('server') and ServiceExists(ServiceName) then
    begin
      WizardForm.StatusLabel.Caption := CustomMessage('StatusStopService');
      StopService(ServiceName);
    end;
    if TunnelConfigured and ServiceExists(CloudflaredService) and ServiceIsOurs(CloudflaredService, WebAppDir + '\Tunnel') then
      StopService(CloudflaredService);
  end;
end;

procedure InstallServer;
var
  RC: Integer;
  Pwd, ExePath, Bin, Sc, Port, LogDir: String;
  Started: Boolean;
begin
  Pwd := '';
  if NeedAdminPassword then Pwd := ServerPage.Values[1];
  Port := IntToStr(ServerPort);
  LogDir := DataDir('') + '\logs';

  WizardForm.StatusLabel.Caption := CustomMessage('StatusConfig');
  ForceDirectories(DataDir('') + '\zaxira');
  ForceDirectories(LogDir);
  WriteServerConfig(Pwd);
  WriteLocalConfigIfMissing;

  if NoSystem then
  begin
    SaveStringToFile(ExpandConstant('{app}\nosystem.flag'), '1', False);
    Log('NOSYSTEM: xizmat, xavfsizlik devori, ruxsatlar va cloudflared o''tkazib yuborildi.');
    Exit;
  end;

  // Maxfiy fayllar va ma'lumotlar: faqat SYSTEM va Administrators (SID bo'yicha — tilga bog'liq emas).
  RunCmd(ExpandConstant('{sys}\icacls.exe'), '"' + ExpandConstant('{app}\Server\appsettings.Production.json') + '" /inheritance:r /grant:r "*S-1-5-18:F" "*S-1-5-32-544:F"', RC);
  if RC <> 0 then AddWarning(FmtMessage(CustomMessage('WarnAcl'), [IntToStr(RC)]));
  RunCmd(ExpandConstant('{sys}\icacls.exe'), '"' + DataDir('') + '" /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F"', RC);
  if RC <> 0 then AddWarning(FmtMessage(CustomMessage('WarnAcl'), [IntToStr(RC)]));

  // Windows xizmati
  WizardForm.StatusLabel.Caption := CustomMessage('StatusService');
  Sc := ExpandConstant('{sys}\sc.exe');
  ExePath := ExpandConstant('{app}\Server\FuelControl.Api.exe');
  // sc: binPath= qiymati bitta argument; ichidagi qo'shtirnoqlar \" bilan (papka nomida bo'sh joy bo'lishi mumkin).
  Bin := 'binPath= "\"' + ExePath + '\"" start= auto DisplayName= "FuelControl"';
  if ServiceExists(ServiceName) then RunCmd(Sc, 'config ' + ServiceName + ' ' + Bin, RC)
  else RunCmd(Sc, 'create ' + ServiceName + ' ' + Bin, RC);
  if RC <> 0 then
    AddWarning(FmtMessage(CustomMessage('WarnServiceCreate'), [IntToStr(RC)]))
  else
  begin
    RunCmd(Sc, 'description ' + ServiceName + ' "FuelControl API va web (Ovoza dasturlar)"', RC);
    // Xatoda qayta ishga tushsin: 5 s, 10 s, keyin har safar 60 s; hisoblagich 1 kunda nollanadi.
    RunCmd(Sc, 'failure ' + ServiceName + ' reset= 86400 actions= restart/5000/restart/10000/restart/60000', RC);
    RunCmd(Sc, 'failureflag ' + ServiceName + ' 1', RC);
  end;

  // Xavfsizlik devori: eski qoida (port o'zgargan bo'lishi mumkin) o'chiriladi, yangisi qo'shiladi.
  WizardForm.StatusLabel.Caption := CustomMessage('StatusFirewall');
  RunCmd(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall delete rule name="' + FirewallRuleName + '"', RC);
  RunCmd(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall add rule name="' + FirewallRuleName + '" dir=in action=allow protocol=TCP localport=' + Port, RC);
  if RC <> 0 then AddWarning(FmtMessage(CustomMessage('WarnFirewall'), [IntToStr(RC), Port]));

  // Ishga tushirish va tekshirish
  WizardForm.StatusLabel.Caption := CustomMessage('StatusStart');
  Started := RunCmd(ExpandConstant('{sys}\net.exe'), 'start "' + ServiceName + '"', RC) and (RC = 0);
  if not Started then
    AddWarning(FmtMessage(CustomMessage('WarnServiceStart'), [IntToStr(RC), LogDir]))
  else if not WaitForApi(ServerPort, 60) then
    AddWarning(FmtMessage(CustomMessage('WarnApiNoAnswer'), [Port, LogDir]))
  else if Pwd <> '' then
    // API javob berdi: baza yaratilib admin foydalanuvchi qo'shildi — parolni sozlama faylidan olib tashlaymiz.
    WriteServerConfig('');
end;

procedure InstallTunnel;
var
  RC: Integer;
  Exe: String;
begin
  WizardForm.StatusLabel.Caption := CustomMessage('StatusTunnel');
  Exe := ExpandConstant('{app}\Tunnel\cloudflared.exe');
  if ServiceExists(CloudflaredService) then
  begin
    if not ServiceIsOurs(CloudflaredService, ExpandConstant('{app}\Tunnel')) then
    begin
      AddWarning(CustomMessage('WarnTunnelExists'));
      Exit;
    end;
    RunCmd(Exe, 'service uninstall', RC);
  end;
  RunCmd(Exe, 'service install ' + TunnelToken, RC);
  if RC <> 0 then AddWarning(FmtMessage(CustomMessage('WarnTunnelFail'), [IntToStr(RC)]));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsComponentSelected('server') then InstallServer;
    if WizardIsComponentSelected('desktop') then WriteDesktopSettings;
    if TunnelConfigured and not NoSystem then InstallTunnel;
    if Warnings <> '' then
      SuppressibleMsgBox(CustomMessage('WarnHeader') + #13#10 + Warnings, mbInformation, MB_OK, IDOK);
  end;
end;

{ ---------- o'chirish ---------- }

var
  UninstallNoSystem: Boolean;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  RC: Integer;
  App: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    App := ExpandConstant('{app}');
    UninstallNoSystem := FileExists(App + '\nosystem.flag');
    // Faqat administrator, faqat "nosystem" emas va faqat shu papkadagi xizmatlar (boshqa nusxaga tegmaymiz).
    if IsAdmin and not UninstallNoSystem then
    begin
      if ServiceExists(CloudflaredService) and ServiceIsOurs(CloudflaredService, App + '\Tunnel') and FileExists(App + '\Tunnel\cloudflared.exe') then
      begin
        StopService(CloudflaredService);
        RunCmd(App + '\Tunnel\cloudflared.exe', 'service uninstall', RC);
      end;
      if ServiceExists(ServiceName) and ServiceIsOurs(ServiceName, App + '\Server') then
      begin
        StopService(ServiceName);
        RunCmd(ExpandConstant('{sys}\sc.exe'), 'delete ' + ServiceName, RC);
        RunCmd(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall delete rule name="' + FirewallRuleName + '"', RC);
      end;
    end;
  end
  else if (CurUninstallStep = usDone) and not UninstallNoSystem then
    SuppressibleMsgBox(FmtMessage(CustomMessage('UninstalledKeepData'), [DataDir('')]), mbInformation, MB_OK, IDOK);
end;
