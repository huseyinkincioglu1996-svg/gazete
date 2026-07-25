const mongoose = require('mongoose');

const deliverySchema = new mongoose.Schema({
  distributor_id: {
    type: mongoose.Schema.Types.ObjectId,
    ref: 'Distributor',
    required: true
  },
  tarih: {
    type: Date,
    required: true
  },
  gun: {
    type: Number,
    required: true // 0-6 (Pzt-Pazar)
  },
  gazeteSayisi: {
    type: Number,
    required: true,
    default: 0
  },
  tutar: {
    type: Number,
    required: true,
    default: 0
  },
  durum: {
    type: String,
    enum: ['Beklemede', 'Tamamlandı', 'İptal'],
    default: 'Beklemede'
  },
  notlar: {
    type: String,
    default: ''
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

module.exports = mongoose.model('Delivery', deliverySchema);
