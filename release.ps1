<#
  EdgeBook yeni sürüm yayınlama betiği (Velopack + GitHub Releases)

  Ne yapar:
    1. Güvenlik kontrolü: appsettings.Local.json var mı, içinde Gemini yedek anahtarı DOLU mu? (doluysa durur,
       çünkü o dosya kurulum paketine girer ve herkes senin anahtarını kullanırdı)
    2. Uygulamayı Release modunda, .NET kurulu olmayan bilgisayarlarda da çalışacak şekilde derler (publish/)
    3. GitHub'daki önceki sürümü indirir (kullanıcılar sadece değişen kısmı indirsin diye; ilk sürümde atlanır)
    4. Velopack ile kurulum dosyası (EdgeBookApp-win-Setup.exe) ve güncelleme paketlerini üretir (Releases/)
    5. Hepsini GitHub'da "v<sürüm>" etiketli yeni bir Release olarak yayınlar
       → Kurulu tüm EdgeBook'lar bir sonraki açılışta "yeni sürüm yayında" şeridini gösterir.

  Bir kez yapılacak hazırlık:
    - vpk aracını kur:            dotnet tool install -g vpk
    - GitHub token oluştur:       GitHub > Settings > Developer settings > Fine-grained tokens > Generate
                                  Repository access: sadece "edgebook"; Permissions > Contents: Read and write
    - Token'ı bu PowerShell oturumuna ver (dosyaya YAZMA):   $env:GITHUB_TOKEN = "github_pat_..."
    - Repo herkese açık olmalı (uygulamalar güncellemeyi token olmadan okuyabilsin).

  Kullanım (repo klasöründe, PowerShell):
    .\release.ps1 -Version 1.0.0
    .\release.ps1 -Version 1.0.1
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoUrl = 'https://github.com/YasirCelikStack/edgebook'
$packId = 'EdgeBookApp'   # Kurulum klasörü %LocalAppData%\EdgeBookApp olur. "EdgeBook" OLMAMALI:
                          # veritabanı %LocalAppData%\EdgeBook'ta; kaldırırken kullanıcının verisi silinirdi.
$uiProject = 'TradingJournal.UI/TradingJournal.UI.csproj'
$localSettings = 'TradingJournal.UI/appsettings.Local.json'

Write-Host "== EdgeBook v$Version yayınlanıyor ==" -ForegroundColor Cyan

# 1) Güvenlik kontrolleri
if (-not $env:GITHUB_TOKEN) { throw 'GITHUB_TOKEN tanımlı değil. Önce: $env:GITHUB_TOKEN = "github_pat_..."' }
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) { throw 'vpk bulunamadı. Önce: dotnet tool install -g vpk' }
if (-not (Test-Path $localSettings)) {
    throw "$localSettings yok. Bu dosya olmadan kurulu sürümde Chart Vault aktarımı çalışmaz."
}
$settingsText = Get-Content $localSettings -Raw
if ($settingsText -match '"GeminiApiKey"\s*:\s*"[^"]+"') {
    throw "appsettings.Local.json içinde GeminiApiKey DOLU. Dağıtılan pakete girer; önce boşalt (`"GeminiApiKey`": `"`")."
}
if (git status --porcelain) {
    Write-Warning 'Commit edilmemiş değişiklikler var. Yayınlanan sürüm bu değişiklikleri de içerecek.'
}

# 2) Derle (self-contained: kullanıcının bilgisayarında .NET kurulu olmasına gerek yok)
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish $uiProject -c Release -r win-x64 --self-contained true -p:Version=$Version -o publish
if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }

# 3) Önceki sürümü indir (delta paketleri için). İlk sürümde GitHub'da sürüm olmadığı için hata verebilir; sorun değil.
vpk download github --repoUrl $repoUrl --token $env:GITHUB_TOKEN
if ($LASTEXITCODE -ne 0) { Write-Host 'Önceki sürüm bulunamadı (ilk sürüm olabilir), devam ediliyor.' -ForegroundColor Yellow }

# 4) Paketle: kurulum dosyası + güncelleme paketleri (Releases klasörüne)
vpk pack --packId $packId --packVersion $Version --packDir publish --mainExe EdgeBook.exe --packTitle EdgeBook --packAuthors 'Yasir Celik' --icon "$PSScriptRoot\TradingJournal.UI\Assets\edgebook.ico" --noPortable
if ($LASTEXITCODE -ne 0) { throw 'Paketleme başarısız.' }

# 5) GitHub'a yükle ve yayınla
vpk upload github --repoUrl $repoUrl --token $env:GITHUB_TOKEN --publish --releaseName "EdgeBook $Version" --tag "v$Version"
if ($LASTEXITCODE -ne 0) { throw 'GitHub yüklemesi başarısız.' }

Write-Host ''
Write-Host "Tamam! EdgeBook v$Version yayında: $repoUrl/releases" -ForegroundColor Green
Write-Host 'Yeni kullanıcılar: Releases sayfasındaki EdgeBookApp-win-Setup.exe ile kurar.'
Write-Host 'Mevcut kullanıcılar: uygulamayı açınca üstte "yeni sürüm yayında" şeridini görür.'
