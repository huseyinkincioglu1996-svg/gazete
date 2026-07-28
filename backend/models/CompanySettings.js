const mongoose = require('mongoose');
const {
  MAX_IMAGE_DATA_URL_LENGTH,
  isValidImageDataUrl
} = require('../utils/imageDataUrl');

const companySettingsSchema = new mongoose.Schema(
  {
    singleton_key: {
      type: String,
      default: 'company',
      enum: ['company'],
      immutable: true,
      unique: true,
      select: false
    },
    firma_logosu: {
      type: String,
      default: null,
      maxlength: MAX_IMAGE_DATA_URL_LENGTH,
      validate: {
        validator: (value) => value === null || isValidImageDataUrl(value),
        message: 'firma_logosu geçerli bir PNG, JPEG veya WebP data URL olmalıdır'
      }
    },
    vitrin_dagitici_id: {
      type: mongoose.Schema.Types.ObjectId,
      ref: 'Distributor',
      default: null
    }
  },
  {
    timestamps: true,
    strict: 'throw'
  }
);

module.exports = mongoose.model('CompanySettings', companySettingsSchema);
