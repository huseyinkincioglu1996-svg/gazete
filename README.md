# Gazete Dağıtım ve Ödeme Takip Sistemi

> Yeni ana uygulama C# / ASP.NET Core MVC / .NET 9 ve Microsoft SQL Server ile
> `src/GazeteDagitim.Web` altında çalışır. Kurulum ve çalıştırma adımları için
> [README-DOTNET.md](README-DOTNET.md) dosyasını kullanın. Eski React/Express/MongoDB
> sürümü geçiş güvenliği için bu depoda korunmaktadır.

Gazete dağıtıcılarını, teslimat kayıtlarını ve ödeme süreçlerini yöneten React, Express ve MongoDB tabanlı bir uygulama.

## Hızlı başlangıç (Docker gerektirmez)

Gereksinimler:

- Node.js 18 veya üzeri
- npm
- Yerel MongoDB Community Server **veya** erişebildiğiniz bir MongoDB bağlantı adresi

Önce ortam dosyasını oluşturun. Gerçek bağlantı bilgileri yalnızca yerel `backend/.env` dosyasında kalmalıdır:

```powershell
Copy-Item backend/.env.example backend/.env
```

`backend/.env` içindeki `MONGODB_URI` değerini kendi MongoDB kurulumunuza göre ayarlayın. Varsayılan örnek, Docker kullanmadan yerelde çalışan MongoDB içindir:

```dotenv
PORT=5000
MONGODB_URI=mongodb://127.0.0.1:27017/gazete-dagitim
DISABLE_CRON=true
```

İsteğe bağlı frontend ayarlarını oluşturun:

```powershell
Copy-Item frontend/.env.example frontend/.env.local
```

Form içinde tıklanabilir Google Haritası kullanmak isterseniz `frontend/.env.local`
dosyasındaki `REACT_APP_GOOGLE_MAPS_API_KEY` değerini kendi tarayıcı anahtarınızla
doldurun. Anahtar olmadan cihaz konumu, koordinat girişi ve Google Maps bağlantısından
konum alma özellikleri çalışmaya devam eder.

Yerel MongoDB Community Server kullanıyorsanız hizmetini başlatın. Hizmet olarak kurulmadıysa örnek bir PowerShell komutu:

```powershell
New-Item -ItemType Directory -Force .\data\db
mongod --dbpath .\data\db
```

MongoDB Atlas gibi uzaktaki bir veritabanını kullanmak da mümkündür; bu durumda `MONGODB_URI` değerine kendi bağlantı adresinizi yazın. Bağlantı adresini, kullanıcı adı/parolayı veya başka bir sırrı Git'e eklemeyin.

Bağımlılıkları modül dizinlerinde kurun:

```powershell
npm --prefix backend install
npm --prefix frontend install
```

Bilgisayarda MongoDB kurulu değilse proje içindeki kalıcı yerel veritabanını ve API'yi
iki ayrı terminalde başlatabilirsiniz:

```powershell
npm --prefix backend run local:db
npm --prefix backend run local:server
```

Yerel veriler `backend/.local-mongodb/data` dizininde korunur. Üçüncü terminalde
arayüzü başlatın:

```powershell
npm --prefix frontend start
```

Bu yerel akışta arayüz `http://127.0.0.1:3001`, API ise
`http://127.0.0.1:5001` adresinde çalışır.

Kendi MongoDB sunucunuzu kullanıyorsanız iki ayrı terminal açın.

Birinci terminalde API'yi çalıştırın:

```powershell
cd backend
npm run dev
```

İkinci terminalde arayüzü çalıştırın:

```powershell
cd frontend
npm start
```

Arayüz varsayılan olarak `http://localhost:3000`, API ise `http://localhost:5000` adresinde açılır. API durumunu `http://localhost:5000/api/test` üzerinden kontrol edebilirsiniz.

> Kök dizindeki `server.js` ve `package.json`, erken dönem iskeletinden kalmıştır; aktif uygulama giriş noktaları `backend/server.js` ve `frontend` dizinidir. Uygulamayı kökten `node server.js` ile çalıştırmayın; yukarıdaki iki terminal komutunu kullanın.

## Ortam değişkenleri

| Değişken | Gerekli | Açıklama |
| --- | --- | --- |
| `PORT` | Hayır | Backend HTTP portu. Varsayılanı `5000`dür. |
| `HOST` | Hayır | Backend dinleme adresi. Güvenlik için varsayılanı `127.0.0.1`dir; dış ortamda bilinçli olarak `0.0.0.0` ayarlanabilir. |
| `MONGODB_URI` | Yerel olmayan veritabanı için evet | MongoDB bağlantı adresi. Ayarlanmazsa `mongodb://localhost:27017/gazete-dagitim` kullanılır. |
| `DISABLE_CRON` | Hayır | `true` olduğunda otomatik teslimat ve ödeme zamanlayıcılarını başlatmaz. Yerel/elle testte önerilir. |
| `DNS_SERVERS` | Hayır | Atlas SRV sorguları sistem DNS'inde çalışmıyorsa kullanılacak virgülle ayrılmış DNS sunucuları. Normalde boş bırakılır. |
| `REACT_APP_API_URL` | Hayır | Arayüzün API adresini geçersiz kılar. Boş bırakıldığında yerelde `http://localhost:5000` kullanılır. |
| `REACT_APP_GOOGLE_MAPS_API_KEY` | Hayır | Abone formundaki tıklanabilir Google Haritasını etkinleştirir. Boş bırakıldığında koordinat ve bağlantı tabanlı seçim kullanılır. |

`backend/.env` Git izleminden çıkarılmıştır ve `.gitignore` ile korunur. Daha önce bir gerçek sır sürüm geçmişine işlendi ise ilgili sırrı yenileyin; yalnızca dosyayı silmek geçmişteki değeri geçersiz kılmaz.

## Doğrulama komutları

Arayüz üretim paketi oluşturma:

```powershell
npm --prefix frontend run build
```

Arayüz testlerini tek sefer çalıştırma:

```powershell
npm --prefix frontend test -- --watchAll=false
```

Backend otomatik testleri:

```powershell
npm --prefix backend test
```

Başlangıç kontrolü için MongoDB çalışırken `npm --prefix backend run start` komutunu kullanın ve `GET /api/health` isteğinin başarılı yanıt verdiğini doğrulayın.

## Proje mimarisi

```text
gazete/
├── backend/
│   ├── server.js          Express API, MongoDB bağlantısı ve zamanlayıcıların başlangıcı
│   ├── routes/            Dağıtıcı, teslimat, ödeme ve rapor HTTP uçları
│   ├── models/            Mongoose veri modelleri
│   ├── cron/              Otomatik teslimat/ödeme görevleri
│   └── .env               Sadece yerel, Git tarafından izlenmez
├── frontend/
│   └── src/               React arayüzü ve API istemcisi
└── README.md
```

Ana API alanları:

- `GET|POST|PUT /api/subscribers` ve `PATCH /api/subscribers/:id/status`
- `GET|POST|PUT|DELETE /api/payment-periods` ve `PATCH /api/payment-periods/:id/status`
- `GET|PUT /api/subscriber-deliveries/daily/:tarih` ve `GET /api/subscriber-deliveries/collections`
- `GET|POST|PUT|DELETE /api/distributors`
- `GET|POST|PUT|DELETE /api/deliveries`
- `GET|POST|PUT|DELETE /api/payments`, `PUT /api/payments/:id/pay` ve aylık/dağıtıcı bazlı `GET /api/payments/tracking`
- `GET|PUT /api/cash-handovers/daily/:tarih` ve `GET /api/cash-handovers/monthly/:YYYY-MM`
- `GET /api/reports/daily/:tarih`, `range`, `zone` ve `distributor` raporları

## Zamanlanmış işler

Backend, otomatik teslimat ve ödeme görevlerini zamanlayıcı üzerinden çalıştırır. Yerel geliştirmede beklenmedik kayıt üretimini önlemek için örnek ortam dosyasında `DISABLE_CRON=true` bulunur. Bu işleri etkinleştirmek istediğiniz ortamda değişkeni `false` yapın veya kaldırın.

## Güvenlik notu

`.env.example` sadece örnek değerler içerir. Gerçek URI, parola, token veya kişisel verileri hiçbir zaman örnek dosyalara, README'ye ya da Git'e koymayın.
