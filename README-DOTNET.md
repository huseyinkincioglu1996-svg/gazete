# Gazete Dağıtım — .NET 9 MVC ve MSSQL

Bu klasördeki yeni ana uygulama:

- C# ve ASP.NET Core MVC
- .NET 9
- Entity Framework Core 9
- Microsoft SQL Server 2022 Express
- Razor Views, JavaScript ve responsive CSS

ile çalışır. Eski React/Express/MongoDB uygulaması veri ve davranış karşılaştırması
için silinmemiştir.

## Proje yapısı

```text
GazeteDagitim.sln
src/GazeteDagitim.Web/
├── Controllers/          MVC controller sınıfları
├── Data/                 EF Core DbContext ve migration dosyaları
├── Infrastructure/       Zamanlanmış iş çalıştırıcısı
├── Models/               MSSQL entity, enum ve ViewModel sınıfları
├── Services/             Dağıtım, ödeme, kasa ve rapor iş kuralları
├── Views/                Razor MVC ekranları
└── wwwroot/              Kırmızı-siyah responsive CSS ve JavaScript
tests/GazeteDagitim.Tests/
scripts/GazeteDagitim.sql İdempotent MSSQL şema betiği
```

## Yerel gereksinimler

- .NET 9 SDK
- Microsoft SQL Server 2022 Express (`.\SQLEXPRESS`)

Projeye özel .NET SDK `.dotnet` klasörüne kurulabilir. Bu klasör Git tarafından
izlenmez. Kurulu sistem SDK’sı varsa normal `dotnet` komutları da kullanılabilir.

> .NET 9 desteği 12 Mayıs 2026 tarihinde sona ermiştir. Bu proje istek gereği
> `net9.0` hedefler. İnternete açık üretim kurulumu öncesinde .NET 10 LTS’ye
> yükseltme planlanmalıdır.

## Veritabanı bağlantısı

Varsayılan geliştirme bağlantısı:

```text
Server=.\SQLEXPRESS;Database=GazeteDagitim;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Bağlantı `src/GazeteDagitim.Web/appsettings.Development.json` dosyasında bulunur.
Kullanıcı adı/parola gereken bir sunucuda gerçek bilgileri Git’e yazmayın; User
Secrets veya `ConnectionStrings__GazeteDagitim` ortam değişkenini kullanın.

Uygulama başlangıcında EF Core migration dosyaları otomatik uygulanır. Elle
uygulamak için:

```powershell
.\.dotnet\dotnet.exe tool restore
.\.dotnet\dotnet.exe tool run dotnet-ef database update `
  --project .\src\GazeteDagitim.Web\GazeteDagitim.Web.csproj `
  --startup-project .\src\GazeteDagitim.Web\GazeteDagitim.Web.csproj
```

Alternatif olarak `scripts/GazeteDagitim.sql` dosyası SQL Server üzerinde
çalıştırılabilir.

## Çalıştırma

```powershell
.\.dotnet\dotnet.exe restore .\GazeteDagitim.sln
.\.dotnet\dotnet.exe run `
  --project .\src\GazeteDagitim.Web\GazeteDagitim.Web.csproj `
  --launch-profile http
```

Uygulama `http://127.0.0.1:5051/menu` adresinde açılır.

## Yerel güvenlik kapsamı

Bu sürüm yalnızca bilgisayar üzerindeki `localhost` / `127.0.0.1` kullanımı için
hazırlanmıştır ve kullanıcı girişi içermez. Uygulamayı yerel ağda veya internette
yayınlamadan önce ASP.NET Core Identity ya da eşdeğer bir kimlik doğrulama ve
yetkilendirme katmanı eklenmelidir.

## Doğrulama

```powershell
.\.dotnet\dotnet.exe build .\GazeteDagitim.sln
.\.dotnet\dotnet.exe test .\GazeteDagitim.sln
```

## Zamanlanmış işler

Günlük dağıtım ve dağıtıcı ödeme kayıtları idempotent servislerle üretilebilir.
Otomatik çalıştırmayı etkinleştirmek için:

```json
{
  "ScheduledJobs": {
    "Enabled": true
  }
}
```

Yerel geliştirmede beklenmedik kayıt oluşmaması için varsayılan değer `false`tur.
