const mongoose = require('mongoose');
const { CASH_HANDOVER_STATUSES } = require('../utils/constants');
const { startOfDay } = require('../utils/date');
const { calculateCashHandoverTotal } = require('../services/cashHandovers');

const cashHandoverItemSchema = new mongoose.Schema(
  {
    abone: {
      type: String,
      required: true,
      trim: true,
      maxlength: 200
    },
    tutar: {
      type: Number,
      required: true,
      min: 0
    },
    aciklama: {
      type: String,
      default: '',
      trim: true,
      maxlength: 1000
    }
  },
  {
    _id: false,
    strict: 'throw'
  }
);

const cashHandoverSchema = new mongoose.Schema(
  {
    // One document represents one calendar day in the Turkish business zone.
    tarih: {
      type: Date,
      required: true
    },
    kalemler: {
      type: [cashHandoverItemSchema],
      default: []
    },
    // This value is always derived on the server from kalemler.
    toplam: {
      type: Number,
      required: true,
      default: 0,
      min: 0
    },
    durum: {
      type: String,
      enum: CASH_HANDOVER_STATUSES,
      default: 'Taslak'
    },
    teslim_tarihi: {
      type: Date,
      default: null
    }
  },
  {
    timestamps: true,
    strict: 'throw'
  }
);

cashHandoverSchema.pre('validate', function normalizeCashHandover(next) {
  try {
    this.tarih = startOfDay(this.tarih, 'tarih');
    this.toplam = calculateCashHandoverTotal(this.kalemler);

    if (this.durum === 'Taslak') {
      this.teslim_tarihi = null;
    } else if (this.durum === 'Teslim Edildi' && !this.teslim_tarihi) {
      this.teslim_tarihi = new Date();
    }

    next();
  } catch (error) {
    next(error);
  }
});

cashHandoverSchema.index(
  { tarih: 1 },
  { unique: true, name: 'unique_cash_handover_per_day' }
);
cashHandoverSchema.index({ durum: 1, tarih: 1 });

module.exports = mongoose.model('CashHandover', cashHandoverSchema);
