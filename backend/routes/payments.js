const express = require('express');
const router = express.Router();
const Payment = require('../models/Payment');
const Delivery = require('../models/Delivery');

// GET - Tüm ödemeleri listele
router.get('/', async (req, res) => {
  try {
    const payments = await Payment.find().populate('distributor_id');
    res.json(payments);
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// GET - Bir ödemeyi getir
router.get('/:id', async (req, res) => {
  try {
    const payment = await Payment.findById(req.params.id).populate('distributor_id');
    if (!payment) {
      return res.status(404).json({ hata: 'Ödeme bulunamadı' });
    }
    res.json(payment);
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// POST - Yeni ödeme ekle
router.post('/', async (req, res) => {
  try {
    const { distributor_id, tutar, tarih, donem_baslangic, donem_bitis, aciklama, odeme_turu } = req.body;

    const payment = new Payment({
      distributor_id,
      tutar,
      tarih,
      donem_baslangic,
      donem_bitis,
      aciklama,
      odeme_turu
    });

    await payment.save();
    res.status(201).json(payment);
  } catch (err) {
    res.status(400).json({ hata: err.message });
  }
});

// PUT - Ödemeyi güncelle
router.put('/:id', async (req, res) => {
  try {
    const payment = await Payment.findByIdAndUpdate(
      req.params.id,
      req.body,
      { new: true, runValidators: true }
    );
    
    if (!payment) {
      return res.status(404).json({ hata: 'Ödeme bulunamadı' });
    }
    
    res.json(payment);
  } catch (err) {
    res.status(400).json({ hata: err.message });
  }
});

// PUT - Ödemeyi ödenmiş yap
router.put('/:id/pay', async (req, res) => {
  try {
    const payment = await Payment.findByIdAndUpdate(
      req.params.id,
      { 
        durum: 'Ödendi',
        odeme_tarihi: new Date()
      },
      { new: true, runValidators: true }
    );
    
    if (!payment) {
      return res.status(404).json({ hata: 'Ödeme bulunamadı' });
    }
    
    res.json(payment);
  } catch (err) {
    res.status(400).json({ hata: err.message });
  }
});

// DELETE - Ödemeyi sil
router.delete('/:id', async (req, res) => {
  try {
    const payment = await Payment.findByIdAndDelete(req.params.id);
    
    if (!payment) {
      return res.status(404).json({ hata: 'Ödeme bulunamadı' });
    }
    
    res.json({ mesaj: 'Ödeme silindi' });
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

module.exports = router;
