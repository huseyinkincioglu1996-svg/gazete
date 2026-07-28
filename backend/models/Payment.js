const mongoose = require('mongoose');
const { PAYMENT_TYPES, PAYMENT_STATUSES } = require('../utils/constants');
const { startOfDay } = require('../utils/date');

const paymentSchema = new mongoose.Schema(
  {
    distributor_id: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Distributor',
      required: true,
      index: true
    },
    tutar: {
      type: Number,
      required: true,
      min: 0
    },
    tarih: {
      type: Date,
      required: true
    },
    // Both period boundaries are inclusive calendar dates.
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
      default: '',
      trim: true,
      maxlength: 1000
    },
    odeme_turu: {
      type: String,
      enum: PAYMENT_TYPES,
      required: true
    },
    durum: {
      type: String,
      enum: PAYMENT_STATUSES,
      default: 'Beklemede'
    },
    odeme_tarihi: {
      type: Date,
      default: null
    }
  },
  {
    timestamps: true,
    strict: 'throw'
  }
);

paymentSchema.pre('validate', function normalizePaymentDates(next) {
  try {
    this.tarih = startOfDay(this.tarih, 'tarih');
    this.donem_baslangic = startOfDay(this.donem_baslangic, 'donem_baslangic');
    this.donem_bitis = startOfDay(this.donem_bitis, 'donem_bitis');

    if (this.donem_bitis < this.donem_baslangic) {
      return next(new Error('donem_bitis, donem_baslangic tarihinden önce olamaz'));
    }

    if (this.durum === 'Beklemede') {
      this.odeme_tarihi = null;
    }

    next();
  } catch (error) {
    next(error);
  }
});

// One payment of each type can cover a distributor's exact period only once.
paymentSchema.index(
  { distributor_id: 1, odeme_turu: 1, donem_baslangic: 1, donem_bitis: 1 },
  { unique: true, name: 'unique_payment_per_distributor_period' }
);

paymentSchema.index({ distributor_id: 1, odeme_turu: 1, donem_bitis: -1 });
paymentSchema.index({ tarih: 1, durum: 1 });

module.exports = mongoose.model('Payment', paymentSchema);
