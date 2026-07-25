const mongoose = require('mongoose');

const distributorSchema = new mongoose.Schema({
  isim: {
    type: String,
    required: true,
    trim: true
  },
  adres: {
    type: String,
    required: true,
    trim: true
  },
  telefon: {
    type: String,
    required: true,
    trim: true
  },
  bolge: {
    type: String,
    enum: ['Bölge 1', 'Bölge 2'],
    required: true
  },
  // Dağıtım Günleri (0=Pzt, 1=Salı, 2=Çrş, 3=Prş, 4=Cmt, 5=Pzr, 6=Pazar)
  dagetim_gunleri: {
    type: [Number],
    default: []
  },
  // Ödeme Tipi
  odeme_tipi: {
    type: String,
    enum: ['Günlük', 'Haftalık', 'Aylık'],
    default: 'Günlük'
  },
  // Haftalık Ödeme Günleri
  odeme_gunleri_hafta: {
    type: [Number],
    default: []
  },
  // Aylık Ödeme Günleri
  odeme_gunleri_ay: {
    type: [Number],
    default: []
  },
  // Gazete Fiyatı (₺)
  gazete_fiyat: {
    type: Number,
    required: true,
    default: 5
  },
  aktif: {
    type: Boolean,
    default: true
  },
  createdAt: {
    type: Date,
    default: Date.now
  },
  updatedAt: {
    type: Date,
    default: Date.now
  }
});

module.exports = mongoose.model('Distributor', distributorSchema);
