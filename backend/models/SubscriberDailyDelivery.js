const mongoose = require('mongoose');
const { SUBSCRIBER_PAYMENT_METHODS } = require('../utils/constants');
const { startOfDay } = require('../utils/date');

function hasOneOrTwoDates(dates) {
  return Array.isArray(dates) && dates.length >= 1 && dates.length <= 2;
}

function hasUniqueDates(dates) {
  return (
    Array.isArray(dates) &&
    new Set(dates.map((date) => date.getTime())).size === dates.length
  );
}

const subscriberDailyDeliverySchema = new mongoose.Schema(
  {
    subscriber_id: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Subscriber',
      required: true,
      index: true
    },
    distributor_id: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Distributor',
      default: null
    },
    distributor_adi: {
      type: String,
      default: '',
      trim: true,
      maxlength: 120
    },
    tarih: {
      type: Date,
      required: true
    },
    kapsanan_tarihler: {
      type: [{ type: Date, required: true }],
      required: true,
      validate: [
        {
          validator: hasOneOrTwoDates,
          message: 'kapsanan_tarihler bir veya iki tarih içermelidir'
        },
        {
          validator: hasUniqueDates,
          message: 'kapsanan_tarihler tekrar eden tarih içeremez'
        }
      ]
    },
    gazete_adedi: {
      type: Number,
      required: true,
      enum: [1, 2]
    },
    teslim_edildi: {
      type: Boolean,
      default: false
    },
    tahsil_edildi: {
      type: Boolean,
      default: false
    },
    tutar: {
      type: Number,
      default: 0,
      min: 0,
      validate: {
        validator(value) {
          const collected = typeof this.get === 'function'
            ? this.get('tahsil_edildi')
            : this.tahsil_edildi;
          return !collected || value > 0;
        },
        message: 'tahsil_edildi true iken tutar 0 değerinden büyük olmalıdır'
      }
    },
    odeme_yontemi: {
      type: String,
      enum: SUBSCRIBER_PAYMENT_METHODS,
      default: 'Nakit'
    }
  },
  {
    timestamps: true,
    strict: 'throw'
  }
);

subscriberDailyDeliverySchema.pre('validate', function normalizeDeliveryDates(next) {
  try {
    this.tarih = startOfDay(this.tarih, 'tarih');
    this.kapsanan_tarihler = (this.kapsanan_tarihler || []).map((date) =>
      startOfDay(date, 'kapsanan_tarihler')
    );

    if (this.kapsanan_tarihler.length !== this.gazete_adedi) {
      this.invalidate(
        'kapsanan_tarihler',
        'kapsanan_tarihler sayısı gazete_adedi ile aynı olmalıdır'
      );
    }

    if (
      !this.kapsanan_tarihler.some(
        (coveredDate) => coveredDate.getTime() === this.tarih.getTime()
      )
    ) {
      this.invalidate('kapsanan_tarihler', 'kapsanan_tarihler tarih alanını içermelidir');
    }

    next();
  } catch (error) {
    next(error);
  }
});

subscriberDailyDeliverySchema.index(
  { subscriber_id: 1, tarih: 1 },
  { unique: true, name: 'unique_subscriber_delivery_per_day' }
);
subscriberDailyDeliverySchema.index({ tarih: -1, tahsil_edildi: 1 });
subscriberDailyDeliverySchema.index({
  distributor_id: 1,
  tarih: -1,
  tahsil_edildi: 1,
  odeme_yontemi: 1
});

module.exports = mongoose.model('SubscriberDailyDelivery', subscriberDailyDeliverySchema);
