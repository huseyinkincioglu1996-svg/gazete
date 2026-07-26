const express = require('express');
const router = express.Router();
const Distributor = require('../models/Distributor');

// GET - Tüm dağıtıcıları listele
router.get('/', async (req, res) => {
  try {
    console.log('📋 Dağıtıcılar getiriliyor...');
    const distributors = await Distributor.find();
    console.log('✅ Dağıtıcılar getirildi:', distributors.length);
    res.json(distributors);
  } catch (err) {
    console.error('❌ GET /api/distributors hatası:', err);
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
    console.error('❌ GET /api/distributors/:id hatası:', err);
    res.status(500).json({ hata: err.message });
  }
});

// POST - Yeni dağıtıcı ekle
router.post('/', async (req, res) => {
  try {
    console.log('📝 Yeni dağıtıcı ekleniyor. Body:', req.body);
    
    const { isim, adres, telefon, bolge, gazete_fiyat, odeme_tipi } = req.body;

    // Validasyon
    if (!isim || !adres || !telefon || !bolge) {
      return res.status(400).json({ hata: 'İsim, adres, telefon ve bölge zorunludur!' });
    }

    const distributor = new Distributor({
      isim,
      adres,
      telefon,
      bolge,
      gazete_fiyat: gazete_fiyat || 5,
      odeme_tipi: odeme_tipi || 'Günlük'
    });

    await distributor.save();
    console.log('✅ Dağıtıcı eklendi:', distributor._id);
    res.status(201).json(distributor);
  } catch (err) {
    console.error('❌ POST /api/distributors hatası:', err);
    res.status(400).json({ hata: err.message });
  }
});

// PUT - Dağıtıcıyı güncelle
router.put('/:id', async (req, res) => {
  try {
    console.log('✏️ Dağıtıcı güncelleniyor. ID:', req.params.id, 'Body:', req.body);
    
    const distributor = await Distributor.findByIdAndUpdate(
      req.params.id,
      req.body,
      { new: true, runValidators: true }
    );
    
    if (!distributor) {
      return res.status(404).json({ hata: 'Dağıtıcı bulunamadı' });
    }
    
    console.log('✅ Dağıtıcı güncellendi:', distributor._id);
    res.json(distributor);
  } catch (err) {
    console.error('❌ PUT /api/distributors/:id hatası:', err);
    res.status(400).json({ hata: err.message });
  }
});

// DELETE - Dağıtıcıyı sil
router.delete('/:id', async (req, res) => {
  try {
    console.log('🗑️ Dağıtıcı siliniyor. ID:', req.params.id);
    
    const distributor = await Distributor.findByIdAndDelete(req.params.id);
    
    if (!distributor) {
      return res.status(404).json({ hata: 'Dağıtıcı bulunamadı' });
    }
    
    console.log('✅ Dağıtıcı silindi:', req.params.id);
    res.json({ mesaj: 'Dağıtıcı silindi' });
  } catch (err) {
    console.error('❌ DELETE /api/distributors/:id hatası:', err);
    res.status(500).json({ hata: err.message });
  }
});

module.exports = router;
