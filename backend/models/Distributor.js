const mongoose = require('mongoose');
const { DISTRIBUTOR_ZONES, PAYMENT_TYPES } = require('../utils/constants');
const {
  MAX_IMAGE_DATA_URL_LENGTH,
  isValidImageDataUrl
} = require('../utils/imageDataUrl');

function hasNoDuplicates(values) {
  return Array.isArray(values) && new Set(values).size === values.length;
}

const distributorSchema = new mongoose.Schema(
  {
    isim: {
      type: String,
      required: true,
      trim: true,
      maxlength: 120
    },
    adres: {
      type: String,
      required: true,
      trim: true,
      maxlength: 500
    },
    telefon: {
      type: String,
      required: true,
      trim: true,
      maxlength: 40
    },
    profil_gorseli: {
      type: String,
      default: null,
      maxlength: MAX_IMAGE_DATA_URL_LENGTH,
      validate: {
        validator: (value) => value === null || isValidImageDataUrl(value),
        message: 'profil_gorseli geçerli bir PNG, JPEG veya WebP data URL olmalıdır'
      }
    },
    bolge: {
      type: String,
      enum: DISTRIBUTOR_ZONES,
      required: true
    },
    // İş kuralı: 0=Pazartesi, ... 6=Pazar.
    dagetim_gunleri: {
      type: [{ type: Number, min: 0, max: 6 }],
      default: [],
      validate: {
        validator: hasNoDuplicates,
        message: 'Dağıtım günleri tekrar edemez'
      }
    },
    odeme_tipi: {
      type: String,
      enum: PAYMENT_TYPES,
      default: 'Günlük'
    },
    odeme_gunleri_hafta: {
      type: [{ type: Number, min: 0, max: 6 }],
      default: [],
      validate: {
        validator: hasNoDuplicates,
        message: 'Haftalık ödeme günleri tekrar edemez'
      }
    },
    odeme_gunleri_ay: {
      type: [{ type: Number, min: 1, max: 31 }],
      default: [],
      validate: {
        validator: hasNoDuplicates,
        message: 'Aylık ödeme günleri tekrar edemez'
      }
    },
    gazete_fiyat: {
      type: Number,
      required: true,
      default: 5,
      min: 0
    },
    aktif: {
      type: Boolean,
      default: true
    }
  },
  {
    timestamps: true,
    strict: 'throw'
  }
);

distributorSchema.pre('validate', function normalizeDayLists(next) {
  for (const field of ['dagetim_gunleri', 'odeme_gunleri_hafta', 'odeme_gunleri_ay']) {
    if (Array.isArray(this[field])) {
      this[field] = [...new Set(this[field])].sort((left, right) => left - right);
    }
  }
  next();
});

module.exports = mongoose.model('Distributor', distributorSchema);
