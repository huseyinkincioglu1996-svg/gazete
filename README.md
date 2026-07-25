# Gazete Dağıtım ve Ödeme Takip Sistemi

Modern gazete dağıtım ve ödeme takip uygulaması.

## 🚀 Özellikler

- ✅ Dağıtıcı yönetimi
- ✅ Günlük/Haftalık/Aylık dağıtım takibi
- ✅ Otomatik ödeme hesaplaması
- ✅ Rapor ve analitik
- ✅ Responsive design

## 📋 Gereksinimler

- Node.js 14+
- MongoDB
- npm veya yarn

## 🔧 Kurulum

### Backend

```bash
cd backend
npm install
npm start
```

### Frontend

```bash
cd frontend
npm install
npm start
```

## 📁 Proje Yapısı

```
gazete-dagitim-sistemi/
├── backend/
│   ├── models/              # MongoDB Schemas
│   ├── routes/              # API Routes
│   ├── cron/                # Cron Jobs
│   ├── server.js            # Express Server
│   ├── package.json
│   └── .env
│
├── frontend/
│   ├── src/
│   │   ├── pages/           # React Pages
│   │   ├── App.jsx
│   │   └── index.jsx
│   ├── package.json
│   └── public/
│       └── index.html
│
└── README.md
```

## 🌐 API Endpoints

### Dağıtıcılar
- `GET /api/distributors` - Tüm dağıtıcıları listele
- `GET /api/distributors/:id` - Bir dağıtıcıyı getir
- `POST /api/distributors` - Yeni dağıtıcı ekle
- `PUT /api/distributors/:id` - Dağıtıcıyı güncelle
- `DELETE /api/distributors/:id` - Dağıtıcıyı sil

### Dağıtımlar
- `GET /api/deliveries` - Tüm dağıtımları listele
- `POST /api/deliveries` - Yeni dağıtım ekle
- `PUT /api/deliveries/:id` - Dağıtımı güncelle
- `DELETE /api/deliveries/:id` - Dağıtımı sil

### Ödemeler
- `GET /api/payments` - Tüm ödemeleri listele
- `POST /api/payments` - Yeni ödeme ekle
- `PUT /api/payments/:id` - Ödemeyi güncelle
- `PUT /api/payments/:id/pay` - Ödemeyi tamamla
- `DELETE /api/payments/:id` - Ödemeyi sil

### Raporlar
- `GET /api/reports/daily/:tarih` - Günlük rapor
- `GET /api/reports/range/:baslangic/:bitis` - Tarih aralığı raporu
- `GET /api/reports/zone/:bolge/:baslangic/:bitis` - Bölge raporu
- `GET /api/reports/distributor/:id/:baslangic/:bitis` - Dağıtıcı raporu

## 📊 Cron Jobs

- **Günlük Dağıtım**: Her gün 00:00'de otomatik dağıtım oluştur
- **Günlük Ödeme**: Her gün 23:59'de günlük ödemeleri hesapla
- **Haftalık Ödeme**: Her gün 23:59'de haftalık ödemeleri hesapla
- **Aylık Ödeme**: Her gün 23:59'de aylık ödemeleri hesapla

## 📝 Lisans

MIT

## 👤 Geliştirici

Hüseyin Kıncıoğlu
