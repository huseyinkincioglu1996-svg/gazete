const express = require('express');
const router = express.Router();
const Delivery = require('../models/Delivery');
const Payment = require('../models/Payment');
const Distributor = require('../models/Distributor');

// GET - Günlük Rapor
router.get('/daily/:tarih', async (req, res) => {
  try {
    const tarih = new Date(req.params.tarih);
    const gunBaslangic = new Date(tarih.getFullYear(), tarih.getMonth(), tarih.getDate());
    const gunBitis = new Date(tarih.getFullYear(), tarih.getMonth(), tarih.getDate() + 1);

    const deliveries = await Delivery.find({
      tarih: { $gte: gunBaslangic, $lt: gunBitis }
    }).populate('distributor_id');

    const payments = await Payment.find({
      tarih: { $gte: gunBaslangic, $lt: gunBitis }
    }).populate('distributor_id');

    const totalGazete = deliveries.reduce((sum, d) => sum + d.gazeteSayisi, 0);
    const totalTutar = payments.reduce((sum, p) => sum + p.tutar, 0);
    const totalOdenen = payments.filter(p => p.durum === 'Ödendi').reduce((sum, p) => sum + p.tutar, 0);
    const totalBeklemede = payments.filter(p => p.durum === 'Beklemede').reduce((sum, p) => sum + p.tutar, 0);

    res.json({
      tarih: req.params.tarih,
      deliveries,
      payments,
      ozet: {
        totalGazete,
        totalTutar,
        totalOdenen,
        totalBeklemede,
        tahsilOrani: totalTutar > 0 ? ((totalOdenen / totalTutar) * 100).toFixed(2) : 0
      }
    });
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// GET - Tarih Aralığı Raporu
router.get('/range/:baslangic/:bitis', async (req, res) => {
  try {
    const baslangic = new Date(req.params.baslangic);
    const bitis = new Date(req.params.bitis);
    bitis.setDate(bitis.getDate() + 1);

    const deliveries = await Delivery.find({
      tarih: { $gte: baslangic, $lt: bitis }
    }).populate('distributor_id');

    const payments = await Payment.find({
      tarih: { $gte: baslangic, $lt: bitis }
    }).populate('distributor_id');

    const totalGazete = deliveries.reduce((sum, d) => sum + d.gazeteSayisi, 0);
    const totalTutar = payments.reduce((sum, p) => sum + p.tutar, 0);
    const totalOdenen = payments.filter(p => p.durum === 'Ödendi').reduce((sum, p) => sum + p.tutar, 0);

    res.json({
      baslangic: req.params.baslangic,
      bitis: req.params.bitis,
      deliveries,
      payments,
      ozet: {
        totalGazete,
        totalTutar,
        totalOdenen,
        totalBeklemede: totalTutar - totalOdenen,
        tahsilOrani: totalTutar > 0 ? ((totalOdenen / totalTutar) * 100).toFixed(2) : 0
      }
    });
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// GET - Bölge Raporu
router.get('/zone/:bolge/:baslangic/:bitis', async (req, res) => {
  try {
    const baslangic = new Date(req.params.baslangic);
    const bitis = new Date(req.params.bitis);
    bitis.setDate(bitis.getDate() + 1);

    const distributors = await Distributor.find({ bolge: req.params.bolge });
    const distributorIds = distributors.map(d => d._id);

    const deliveries = await Delivery.find({
      distributor_id: { $in: distributorIds },
      tarih: { $gte: baslangic, $lt: bitis }
    }).populate('distributor_id');

    const payments = await Payment.find({
      distributor_id: { $in: distributorIds },
      tarih: { $gte: baslangic, $lt: bitis }
    }).populate('distributor_id');

    res.json({
      bolge: req.params.bolge,
      deliveries,
      payments
    });
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// GET - Dağıtıcı Raporu
router.get('/distributor/:id/:baslangic/:bitis', async (req, res) => {
  try {
    const baslangic = new Date(req.params.baslangic);
    const bitis = new Date(req.params.bitis);
    bitis.setDate(bitis.getDate() + 1);

    const deliveries = await Delivery.find({
      distributor_id: req.params.id,
      tarih: { $gte: baslangic, $lt: bitis }
    }).populate('distributor_id');

    const payments = await Payment.find({
      distributor_id: req.params.id,
      tarih: { $gte: baslangic, $lt: bitis }
    }).populate('distributor_id');

    const distributor = await Distributor.findById(req.params.id);

    res.json({
      distributor,
      deliveries,
      payments
    });
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

module.exports = router;
