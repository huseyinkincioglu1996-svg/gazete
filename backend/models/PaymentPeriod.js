const mongoose = require('mongoose');

const paymentPeriodSchema = new mongoose.Schema(
  {
    ad: {
      type: String,
      required: true,
      trim: true,
      maxlength: 120
    },
    gun_sayisi: {
      type: Number,
      required: true,
      min: 1,
      max: 365,
      validate: {
        validator: Number.isInteger,
        message: 'gun_sayisi tam sayı olmalıdır'
      }
    },
    aciklama: {
      type: String,
      default: '',
      trim: true,
      maxlength: 500
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

paymentPeriodSchema.index(
  { ad: 1 },
  {
    unique: true,
    name: 'unique_payment_period_name',
    collation: { locale: 'tr', strength: 2 }
  }
);
paymentPeriodSchema.index({ aktif: 1, ad: 1 }, { name: 'payment_period_status_and_name' });

module.exports = mongoose.model('PaymentPeriod', paymentPeriodSchema);
