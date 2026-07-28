const mongoose = require('mongoose');
const { DELIVERY_STATUSES } = require('../utils/constants');
const { startOfDay, getTurkishBusinessDay } = require('../utils/date');

const deliverySchema = new mongoose.Schema(
  {
    distributor_id: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Distributor',
      required: true,
      index: true
    },
    // Each delivery is stored at the start of its local calendar day.
    tarih: {
      type: Date,
      required: true
    },
    // 0=Pazartesi, ... 6=Pazar. This is derived from tarih before validation.
    gun: {
      type: Number,
      required: true,
      min: 0,
      max: 6
    },
    gazeteSayisi: {
      type: Number,
      required: true,
      default: 0,
      min: 0
    },
    tutar: {
      type: Number,
      required: true,
      default: 0,
      min: 0
    },
    durum: {
      type: String,
      enum: DELIVERY_STATUSES,
      default: 'Beklemede'
    },
    notlar: {
      type: String,
      default: '',
      trim: true,
      maxlength: 1000
    }
  },
  {
    timestamps: true,
    strict: 'throw'
  }
);

deliverySchema.pre('validate', function normalizeDeliveryDate(next) {
  try {
    this.tarih = startOfDay(this.tarih, 'tarih');
    this.gun = getTurkishBusinessDay(this.tarih);
    next();
  } catch (error) {
    next(error);
  }
});

// The unique index is the final guard against a cron retry creating two records
// for the same distributor and calendar day.
deliverySchema.index(
  { distributor_id: 1, tarih: 1 },
  { unique: true, name: 'unique_delivery_per_distributor_day' }
);

deliverySchema.index({ tarih: 1, durum: 1 });

module.exports = mongoose.model('Delivery', deliverySchema);
