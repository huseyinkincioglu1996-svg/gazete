const express = require('express');
const router = express.Router();
const Delivery = require('../models/Delivery');
const Payment = require('../models/Payment');
const Distributor = require('../models/Distributor');
const { DISTRIBUTOR_ZONES } = require('../utils/constants');
const { createInclusiveDateRange } = require('../utils/date');
const { asyncHandler, HttpError } = require('../utils/http');
const { enumValue, objectId } = require('../utils/validation');

function reportRange(startValue, endValue) {
  try {
    return createInclusiveDateRange(startValue, endValue);
  } catch (error) {
    throw new HttpError(400, error.message);
  }
}

function summarize(deliveries, payments) {
  const totalGazete = deliveries.reduce((sum, delivery) => sum + delivery.gazeteSayisi, 0);
  const totalTutar = payments.reduce((sum, payment) => sum + payment.tutar, 0);
  const totalOdenen = payments
    .filter((payment) => payment.durum === 'Ödendi')
    .reduce((sum, payment) => sum + payment.tutar, 0);

  return {
    totalGazete,
    totalTutar,
    totalOdenen,
    totalBeklemede: totalTutar - totalOdenen,
    tahsilOrani: totalTutar > 0 ? ((totalOdenen / totalTutar) * 100).toFixed(2) : 0
  };
}

async function findReportData(filter, range) {
  const dateFilter = { tarih: { $gte: range.start, $lt: range.endExclusive } };
  const deliveries = await Delivery.find({ ...filter, ...dateFilter }).populate('distributor_id');
  const payments = await Payment.find({ ...filter, ...dateFilter }).populate('distributor_id');
  return { deliveries, payments };
}

router.get('/daily/:tarih', asyncHandler(async (req, res) => {
  const range = reportRange(req.params.tarih, req.params.tarih);
  const { deliveries, payments } = await findReportData({}, range);

  res.json({
    tarih: req.params.tarih,
    deliveries,
    payments,
    ozet: summarize(deliveries, payments)
  });
}));

router.get('/range/:baslangic/:bitis', asyncHandler(async (req, res) => {
  const range = reportRange(req.params.baslangic, req.params.bitis);
  const { deliveries, payments } = await findReportData({}, range);

  res.json({
    baslangic: req.params.baslangic,
    bitis: req.params.bitis,
    deliveries,
    payments,
    ozet: summarize(deliveries, payments)
  });
}));

router.get('/zone/:bolge/:baslangic/:bitis', asyncHandler(async (req, res) => {
  const bolge = enumValue(req.params.bolge, 'bolge', DISTRIBUTOR_ZONES);
  const range = reportRange(req.params.baslangic, req.params.bitis);
  const distributors = await Distributor.find({ bolge }).select({ _id: 1 });
  const distributorIds = distributors.map((distributor) => distributor._id);
  const { deliveries, payments } = await findReportData({ distributor_id: { $in: distributorIds } }, range);

  res.json({ bolge, deliveries, payments });
}));

router.get('/distributor/:id/:baslangic/:bitis', asyncHandler(async (req, res) => {
  const distributorId = objectId(req.params.id);
  const range = reportRange(req.params.baslangic, req.params.bitis);
  const distributor = await Distributor.findById(distributorId);
  if (!distributor) {
    throw new HttpError(404, 'Dağıtıcı bulunamadı');
  }

  const { deliveries, payments } = await findReportData({ distributor_id: distributorId }, range);
  res.json({ distributor, deliveries, payments });
}));

module.exports = router;
