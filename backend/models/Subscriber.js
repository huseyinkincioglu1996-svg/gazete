const mongoose = require('mongoose');
const { SUBSCRIBER_NEWSPAPER_DAYS } = require('../utils/constants');

const subscriberLocationSchema = new mongoose.Schema(
  {
    enlem: {
      type: Number,
      required: true,
      min: -90,
      max: 90
    },
    boylam: {
      type: Number,
      required: true,
      min: -180,
      max: 180
    }
  },
  {
    _id: false,
    strict: 'throw'
  }
);

function hasUniqueNewspaperDays(days) {
  return Array.isArray(days) && new Set(days).size === days.length;
}

function hasNoSundayMondayConflict(days) {
  return !(
    Array.isArray(days) &&
    days.includes('pazar_pazartesi') &&
    (days.includes('pazar') || days.includes('pazartesi'))
  );
}

const subscriberSchema = new mongoose.Schema(
  {
    isim: {
      type: String,
      required: true,
      trim: true,
      maxlength: 160
    },
    telefon: {
      type: String,
      default: '',
      trim: true,
      maxlength: 40
    },
    adres: {
      type: String,
      default: '',
      trim: true,
      maxlength: 500
    },
    aylik_ucret: {
      type: Number,
      default: 0,
      min: 0
    },
    notlar: {
      type: String,
      default: '',
      trim: true,
      maxlength: 1000
    },
    aktif: {
      type: Boolean,
      default: true
    },
    gazete_gunleri: {
      type: [{
        type: String,
        enum: SUBSCRIBER_NEWSPAPER_DAYS
      }],
      default: [],
      validate: [
        {
          validator: hasUniqueNewspaperDays,
          message: 'gazete_gunleri tekrar eden değer içeremez'
        },
        {
          validator: hasNoSundayMondayConflict,
          message: 'pazar_pazartesi, pazar veya pazartesi ile birlikte seçilemez'
        }
      ]
    },
    odeme_periyodu_id: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'PaymentPeriod',
      default: null,
      index: true
    },
    distributor_id: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Distributor',
      default: null,
      index: true
    },
    konum: {
      type: subscriberLocationSchema,
      default: null
    }
  },
  {
    timestamps: true,
    strict: 'throw'
  }
);

subscriberSchema.index({ isim: 1 }, { name: 'subscriber_name' });
subscriberSchema.index({ aktif: 1, isim: 1 }, { name: 'subscriber_status_and_name' });

module.exports = mongoose.model('Subscriber', subscriberSchema);
