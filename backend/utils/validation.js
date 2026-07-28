const mongoose = require('mongoose');
const { startOfDay, getTurkishBusinessDay } = require('./date');
const { HttpError } = require('./http');
const { inspectImageDataUrl } = require('./imageDataUrl');

const hasOwn = (object, key) => Object.prototype.hasOwnProperty.call(object, key);

function ensureBodyObject(body) {
  if (!body || typeof body !== 'object' || Array.isArray(body)) {
    throw new HttpError(400, 'İstek gövdesi bir nesne olmalıdır');
  }
}

function rejectUnknownFields(body, allowedFields) {
  ensureBodyObject(body);
  const unknownFields = Object.keys(body).filter((field) => !allowedFields.includes(field));

  if (unknownFields.length > 0) {
    throw new HttpError(400, `İzin verilmeyen alanlar: ${unknownFields.join(', ')}`);
  }
}

function requiredString(value, fieldName, { maxLength = 500, required = true } = {}) {
  if (value === undefined || value === null) {
    if (required) {
      throw new HttpError(400, `${fieldName} zorunludur`);
    }
    return undefined;
  }

  if (typeof value !== 'string') {
    throw new HttpError(400, `${fieldName} metin olmalıdır`);
  }

  const normalized = value.trim();
  if (required && !normalized) {
    throw new HttpError(400, `${fieldName} boş olamaz`);
  }

  if (normalized.length > maxLength) {
    throw new HttpError(400, `${fieldName} en fazla ${maxLength} karakter olabilir`);
  }

  return normalized;
}

function finiteNumber(value, fieldName, { min = Number.NEGATIVE_INFINITY, integer = false } = {}) {
  if (value === '' || value === null || value === undefined) {
    throw new HttpError(400, `${fieldName} zorunludur`);
  }

  const normalized = typeof value === 'string' ? Number(value.trim()) : value;
  if (typeof normalized !== 'number' || !Number.isFinite(normalized)) {
    throw new HttpError(400, `${fieldName} geçerli bir sayı olmalıdır`);
  }

  if (normalized < min) {
    throw new HttpError(400, `${fieldName} ${min} değerinden küçük olamaz`);
  }

  if (integer && !Number.isInteger(normalized)) {
    throw new HttpError(400, `${fieldName} tam sayı olmalıdır`);
  }

  return normalized;
}

function enumValue(value, fieldName, values) {
  if (typeof value !== 'string' || !values.includes(value)) {
    throw new HttpError(400, `${fieldName} geçerli bir değer olmalıdır`);
  }

  return value;
}

function booleanValue(value, fieldName) {
  if (typeof value !== 'boolean') {
    throw new HttpError(400, `${fieldName} doğru veya yanlış olmalıdır`);
  }

  return value;
}

function imageDataUrl(value, fieldName, { nullable = true } = {}) {
  if (value === null && nullable) {
    return null;
  }

  try {
    inspectImageDataUrl(value);
  } catch (error) {
    throw new HttpError(400, `${fieldName}: ${error.message}`);
  }

  return value;
}

function dayArray(value, fieldName, { min = 0, max = 6 } = {}) {
  if (!Array.isArray(value)) {
    throw new HttpError(400, `${fieldName} bir dizi olmalıdır`);
  }

  const normalized = value.map((item) => finiteNumber(item, fieldName, { min, integer: true }));
  if (normalized.some((item) => item > max)) {
    throw new HttpError(400, `${fieldName} değerleri ${min}-${max} aralığında olmalıdır`);
  }

  return [...new Set(normalized)].sort((left, right) => left - right);
}

function normalizedDate(value, fieldName) {
  try {
    return startOfDay(value, fieldName);
  } catch (error) {
    throw new HttpError(400, error.message);
  }
}

function objectId(value, fieldName = 'id') {
  if (typeof value !== 'string' || !mongoose.isValidObjectId(value)) {
    throw new HttpError(400, `${fieldName} geçerli bir kimlik olmalıdır`);
  }

  return value;
}

function deliveryDayForDate(date, providedDay) {
  const expectedDay = getTurkishBusinessDay(date);

  if (providedDay !== undefined) {
    const day = finiteNumber(providedDay, 'gun', { min: 0, integer: true });
    if (day > 6 || day !== expectedDay) {
      throw new HttpError(400, 'gun, tarih ile uyumlu olmalıdır (0=Pazartesi, 6=Pazar)');
    }
  }

  return expectedDay;
}

module.exports = {
  hasOwn,
  ensureBodyObject,
  rejectUnknownFields,
  requiredString,
  finiteNumber,
  enumValue,
  booleanValue,
  imageDataUrl,
  dayArray,
  normalizedDate,
  objectId,
  deliveryDayForDate
};
