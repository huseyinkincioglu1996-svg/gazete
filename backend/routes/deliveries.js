const express = require('express');
const router = express.Router();
const Delivery = require('../models/Delivery');
const Distributor = require('../models/Distributor');
const { asyncHandler, HttpError } = require('../utils/http');
const { objectId, deliveryDayForDate, hasOwn } = require('../utils/validation');
const { buildDeliveryPayload } = require('../utils/payloads');

async function assertActiveDistributor(distributorId) {
  const distributor = await Distributor.findById(distributorId).select({ aktif: 1 });
  if (!distributor) {
    throw new HttpError(404, 'Dağıtıcı bulunamadı');
  }
  if (!distributor.aktif) {
    throw new HttpError(409, 'Pasif dağıtıcı için yeni dağıtım oluşturulamaz');
  }
}

function normalizeDeliveryDay(payload, existingDate) {
  const date = payload.tarih || existingDate;
  const suppliedDay = hasOwn(payload, 'gun') ? payload.gun : undefined;
  // Gun is persisted as a derived value even when only tarih changes.
  payload.gun = deliveryDayForDate(date, suppliedDay);
}

router.get('/', asyncHandler(async (req, res) => {
  const deliveries = await Delivery.find().sort({ tarih: -1 }).populate('distributor_id');
  res.json(deliveries);
}));

router.get('/:id', asyncHandler(async (req, res) => {
  const delivery = await Delivery.findById(objectId(req.params.id)).populate('distributor_id');
  if (!delivery) {
    throw new HttpError(404, 'Dağıtım bulunamadı');
  }
  res.json(delivery);
}));

router.post('/', asyncHandler(async (req, res) => {
  const payload = buildDeliveryPayload(req.body);
  await assertActiveDistributor(payload.distributor_id);
  normalizeDeliveryDay(payload, payload.tarih);

  const delivery = new Delivery(payload);
  await delivery.save();
  res.status(201).json(delivery);
}));

router.put('/:id', asyncHandler(async (req, res) => {
  const delivery = await Delivery.findById(objectId(req.params.id));
  if (!delivery) {
    throw new HttpError(404, 'Dağıtım bulunamadı');
  }

  const payload = buildDeliveryPayload(req.body, { partial: true });
  if (hasOwn(payload, 'distributor_id') && String(payload.distributor_id) !== String(delivery.distributor_id)) {
    await assertActiveDistributor(payload.distributor_id);
  }
  normalizeDeliveryDay(payload, delivery.tarih);

  Object.assign(delivery, payload);
  await delivery.save();
  res.json(delivery);
}));

router.delete('/:id', asyncHandler(async (req, res) => {
  const delivery = await Delivery.findByIdAndDelete(objectId(req.params.id));
  if (!delivery) {
    throw new HttpError(404, 'Dağıtım bulunamadı');
  }
  res.json({ mesaj: 'Dağıtım silindi' });
}));

module.exports = router;
