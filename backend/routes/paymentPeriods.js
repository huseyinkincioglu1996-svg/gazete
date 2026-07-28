const express = require('express');
const router = express.Router();
const PaymentPeriod = require('../models/PaymentPeriod');
const Subscriber = require('../models/Subscriber');
const { asyncHandler, HttpError } = require('../utils/http');
const {
  buildPaymentPeriodPayload,
  buildPaymentPeriodStatusPayload
} = require('../utils/payloads');
const { objectId, rejectUnknownFields } = require('../utils/validation');

function activeFilter(query) {
  rejectUnknownFields(query, ['aktif']);
  if (query.aktif === undefined) {
    return {};
  }
  if (query.aktif === 'true') {
    return { aktif: true };
  }
  if (query.aktif === 'false') {
    return { aktif: false };
  }
  throw new HttpError(400, 'aktif sorgu değeri true veya false olmalıdır');
}

router.get('/', asyncHandler(async (req, res) => {
  const periods = await PaymentPeriod.find(activeFilter(req.query)).sort({ ad: 1 });
  res.json(periods);
}));

router.get('/:id', asyncHandler(async (req, res) => {
  const period = await PaymentPeriod.findById(objectId(req.params.id));
  if (!period) {
    throw new HttpError(404, 'Ödeme periyodu bulunamadı');
  }
  res.json(period);
}));

router.post('/', asyncHandler(async (req, res) => {
  const period = new PaymentPeriod(buildPaymentPeriodPayload(req.body));
  await period.save();
  res.status(201).json(period);
}));

router.put('/:id', asyncHandler(async (req, res) => {
  const period = await PaymentPeriod.findById(objectId(req.params.id));
  if (!period) {
    throw new HttpError(404, 'Ödeme periyodu bulunamadı');
  }

  Object.assign(period, buildPaymentPeriodPayload(req.body, { partial: true }));
  await period.save();
  res.json(period);
}));

router.patch('/:id/status', asyncHandler(async (req, res) => {
  const period = await PaymentPeriod.findById(objectId(req.params.id));
  if (!period) {
    throw new HttpError(404, 'Ödeme periyodu bulunamadı');
  }

  Object.assign(period, buildPaymentPeriodStatusPayload(req.body));
  await period.save();
  res.json(period);
}));

router.delete('/:id', asyncHandler(async (req, res) => {
  const periodId = objectId(req.params.id);
  const inUse = await Subscriber.exists({ odeme_periyodu_id: periodId });
  if (inUse) {
    throw new HttpError(
      409,
      'Abonelere atanmış ödeme periyodu silinemez; bunun yerine pasife alın'
    );
  }

  const period = await PaymentPeriod.findByIdAndDelete(periodId);
  if (!period) {
    throw new HttpError(404, 'Ödeme periyodu bulunamadı');
  }
  res.json({ mesaj: 'Ödeme periyodu silindi' });
}));

module.exports = router;
module.exports.activeFilter = activeFilter;
