const mongoose = require('mongoose');

const paymentSchema = new mongoose.Schema({
  distributor_id: {
    type: mongoose.Schema.Types.ObjectId,
    ref: 'Distributor',
    required: true
  },
  tutar: {
    type: Number,
    required: true
  },
  tarih: {
    type: Date,
    required: true
  },
  donem_baslangic: {
    type: Date,
    required: true
  },
  donem_bitis: {
    type: Date,
    required: true
  },
  aciklama: {
    type: String,
    default: ''
  },
  odeme_turu: {
    type: String,
    enum: ['Günlük', 'Haftalık', 'Aylık'],
    required: true
  },
  durum: {
    type: String,
    enum: ['Beklemede', 'Ödendi'],
    default: 'Beklemede'
  },
  odeme_tarihi: {
    type: Date,
    default: null
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

module.exports = mongoose.model('Payment', paymentSchema);
