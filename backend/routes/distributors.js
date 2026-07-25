const express = require('express');
const router = express.Router();
const Distributor = require('../models/Distributor');

// GET - Tüm dağıtıcıları listele
router.get('/', async (req, res) => {
  try {
    const distributors = await Distributor.find();
    res.json(distributors);
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// GET - Bir dağıtıcıyı getir
router.get('/:id', async (req, res) => {
  try {
    const distributor = await Distributor.findById(req.params.id);
    if (!distributor) {
      return res.status(404).json({ hata: 'Dağıtıcı bulunamadı' });
    }
    res.json(distributor);
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

// POST - Yeni dağıtıcı ekle
router.post('/', async (req, res) => {
  try {
    const { isim, adres, telefon, bolge, dagetim_gunleri, odeme_tipi, odeme_gunleri_hafta, odeme_gunleri_ay, gazete_fiyat } = req.body;

    const distributor = new Distributor({
      isim,
      adres,
      telefon,
      bolge,
      dagetim_gunleri,
      odeme_tipi,
      odeme_gunleri_hafta,
      odeme_gunleri_ay,
      gazete_fiyat
    });

    await distributor.save();
    res.status(201).json(distributor);
  } catch (err) {
    res.status(400).json({ hata: err.message });
  }
});

// PUT - Dağıtıcıyı güncelle
router.put('/:id', async (req, res) => {
  try {
    const distributor = await Distributor.findByIdAndUpdate(
      req.params.id,
      req.body,
      { new: true, runValidators: true }
    );
    
    if (!distributor) {
      return res.status(404).json({ hata: 'Dağıtıcı bulunamadı' });
    }
    
    res.json(distributor);
  } catch (err) {
    res.status(400).json({ hata: err.message });
  }
});

// DELETE - Dağıtıcıyı sil
router.delete('/:id', async (req, res) => {
  try {
    const distributor = await Distributor.findByIdAndDelete(req.params.id);
    
    if (!distributor) {
      return res.status(404).json({ hata: 'Dağıtıcı bulunamadı' });
    }
    
    res.json({ mesaj: 'Dağıtıcı silindi' });
  } catch (err) {
    res.status(500).json({ hata: err.message });
  }
});

module.exports = router;
