const express = require('express');
const router = express.Router();
const Delivery = require('../models/Delivery');

// GET - Tüm dağıtımları listele
router.get('/', async (req, res) => {
  try {
    const deliveries = await Delivery.find().populate('distributor_id');
    res.json(deliveries);
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// GET - Bir dağıtımı getir
router.get('/:id', async (req, res) => {
  try {
    const delivery = await Delivery.findById(req.params.id).populate('distributor_id');
    if (!delivery) {
      return res.status(404).json({ hata: 'Dağıtım bulunamadı' });
    }
    res.json(delivery);
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// POST - Yeni dağıtım ekle
router.post('/', async (req, res) => {
  try {
    const { distributor_id, tarih, gun, gazeteSayisi, tutar, durum, notlar } = req.body;

    const delivery = new Delivery({
      distributor_id,
      tarih,
      gun,
      gazeteSayisi,
      tutar,
      durum,
      notlar
    });

    await delivery.save();
    res.status(201).json(delivery);
  } catch (err) {
    res.status(400).json({ hata: err.message });
  }
});

// PUT - Dağıtımı güncelle
router.put('/:id', async (req, res) => {
  try {
    const delivery = await Delivery.findByIdAndUpdate(
      req.params.id,
      req.body,
      { new: true, runValidators: true }
    );
    
    if (!delivery) {
      return res.status(404).json({ hata: 'Dağıtım bulunamadı' });
    }
    
    res.json(delivery);
  } catch (err) {
    res.status(400).json({ hata: err.message });
  }
});

// DELETE - Dağıtımı sil
router.delete('/:id', async (req, res) => {
  try {
    const delivery = await Delivery.findByIdAndDelete(req.params.id);
    
    if (!delivery) {
      return res.status(404).json({ hata: 'Dağıtım bulunamadı' });
    }
    
    res.json({ mesaj: 'Dağıtım silindi' });
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

module.exports = router;
