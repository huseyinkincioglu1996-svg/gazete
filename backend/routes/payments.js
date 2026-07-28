const express = require('express');
const router = express.Router();
const Payment = require('../models/Payment');
const Distributor = require('../models/Distributor');
const SubscriberDailyDelivery = require('../models/SubscriberDailyDelivery');
const { asyncHandler, HttpError } = require('../utils/http');
const { objectId, hasOwn } = require('../utils/validation');
const { buildPaymentPayload } = require('../utils/payloads');
const {
  calculatePaymentTrackingSummary,
  parsePaymentTrackingQuery
} = require('../services/paymentTracking');
const { serializeSubscriberCollection } = require('../services/subscriberDeliveries');

async function assertDistributorExists(distributorId) {
  const distributor = await Distributor.findById(distributorId).select({ _id: 1 });
  if (!distributor) {
    throw new HttpError(404, 'Dağıtıcı bulunamadı');
  }
}

function assertPaymentPeriod(payload, currentPayment) {
  const start = payload.donem_baslangic || currentPayment?.donem_baslangic;
  const end = payload.donem_bitis || currentPayment?.donem_bitis;
  if (start && end && end < start) {
    throw new HttpError(400, 'donem_bitis, donem_baslangic tarihinden önce olamaz');
  }
}

router.get('/tracking', asyncHandler(async (req, res) => {
  const range = parsePaymentTrackingQuery(req.query);
  if (range.distributorId) {
    await assertDistributorExists(range.distributorId);
  }

  const dateFilter = {
    $gte: range.start,
    $lt: range.endExclusive
  };
  const paymentFilter = { tarih: dateFilter };
  const collectionFilter = {
    tarih: dateFilter,
    tahsil_edildi: true,
    odeme_yontemi: 'Nakit'
  };
  if (range.distributorId) {
    paymentFilter.distributor_id = range.distributorId;
    collectionFilter.$or = [
      { distributor_id: range.distributorId },
      { distributor_id: null }
    ];
  }

  const [payments, collectionRecords] = await Promise.all([
    Payment.find(paymentFilter)
      .sort({ tarih: -1 })
      .populate('distributor_id')
      .lean(),
    SubscriberDailyDelivery.find(collectionFilter)
      .sort({ tarih: -1, createdAt: -1 })
      .populate([
        {
          path: 'subscriber_id',
          select: { isim: 1, distributor_id: 1 },
          populate: { path: 'distributor_id', select: { isim: 1 } }
        },
        {
          path: 'distributor_id',
          select: { isim: 1 }
        }
      ])
      .lean()
  ]);

  const cashCollections = collectionRecords
    .map(serializeSubscriberCollection)
    .filter(
      (collection) =>
        !range.distributorId ||
        String(collection.distributor_id || '') === range.distributorId
    );

  res.json({
    ay: range.month,
    distributor_id: range.distributorId,
    ozet: calculatePaymentTrackingSummary(payments, cashCollections),
    odemeler: payments,
    nakit_tahsilatlar: cashCollections
  });
}));

router.get('/', asyncHandler(async (req, res) => {
  const payments = await Payment.find().sort({ tarih: -1 }).populate('distributor_id');
  res.json(payments);
}));

router.get('/:id', asyncHandler(async (req, res) => {
  const payment = await Payment.findById(objectId(req.params.id)).populate('distributor_id');
  if (!payment) {
    throw new HttpError(404, 'Ödeme bulunamadı');
  }
  res.json(payment);
}));

router.post('/', asyncHandler(async (req, res) => {
  const payload = buildPaymentPayload(req.body);
  await assertDistributorExists(payload.distributor_id);
  assertPaymentPeriod(payload);

  const payment = new Payment(payload);
  await payment.save();
  res.status(201).json(payment);
}));

router.put('/:id', asyncHandler(async (req, res) => {
  const payment = await Payment.findById(objectId(req.params.id));
  if (!payment) {
    throw new HttpError(404, 'Ödeme bulunamadı');
  }

  const payload = buildPaymentPayload(req.body, { partial: true, allowStatus: true });
  if (hasOwn(payload, 'distributor_id') && String(payload.distributor_id) !== String(payment.distributor_id)) {
    await assertDistributorExists(payload.distributor_id);
  }
  assertPaymentPeriod(payload, payment);

  if (payload.durum === 'Ödendi' && !hasOwn(payload, 'odeme_tarihi')) {
    payload.odeme_tarihi = new Date();
  }
  if (payload.durum === 'Beklemede') {
    payload.odeme_tarihi = null;
  }

  Object.assign(payment, payload);
  await payment.save();
  res.json(payment);
}));

router.put('/:id/pay', asyncHandler(async (req, res) => {
  const payment = await Payment.findById(objectId(req.params.id));
  if (!payment) {
    throw new HttpError(404, 'Ödeme bulunamadı');
  }

  if (payment.durum !== 'Ödendi') {
    payment.durum = 'Ödendi';
    payment.odeme_tarihi = new Date();
    await payment.save();
  }

  res.json(payment);
}));

router.delete('/:id', asyncHandler(async (req, res) => {
  const payment = await Payment.findById(objectId(req.params.id));
  if (!payment) {
    throw new HttpError(404, 'Ödeme bulunamadı');
  }
  if (payment.durum === 'Ödendi') {
    throw new HttpError(409, 'Ödenmiş bir ödeme silinemez; finansal geçmiş korunur');
  }

  await payment.deleteOne();
  res.json({ mesaj: 'Ödeme silindi' });
}));

module.exports = router;
