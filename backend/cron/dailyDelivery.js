const Distributor = require('../models/Distributor');
const Delivery = require('../models/Delivery');

// Her gün 00:00'de çalışır - Otomatik dağıtım oluştur
async function dailyDeliveryCron() {
  try {
    console.log('🔄 Günlük dağıtım kontrolü çalışıyor...');

    const today = new Date();
    const dayOfWeek = today.getDay(); // 0=Pzt, 1=Salı, ..., 6=Pazar
    const gunBaslangic = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const gunBitis = new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1);

    // Bugünü dağıtım yapan dağıtıcıları bul
    const distributors = await Distributor.find({
      aktif: true,
      dagetim_gunleri: dayOfWeek
    });

    for (const distributor of distributors) {
      // Bugün için dağıtım kaydı olup olmadığını kontrol et
      const existingDelivery = await Delivery.findOne({
        distributor_id: distributor._id,
        tarih: { $gte: gunBaslangic, $lt: gunBitis }
      });

      if (!existingDelivery) {
        // Yeni dağıtım oluştur
        const delivery = new Delivery({
          distributor_id: distributor._id,
          tarih: today,
          gun: dayOfWeek,
          gazeteSayisi: 0,
          tutar: 0,
          durum: 'Beklemede'
        });

        await delivery.save();
        console.log(`✅ ${distributor.isim} için dağıtım oluşturuldu`);
      }
    }

    console.log('✅ Günlük dağıtım kontrolü tamamlandı');
  } catch (err) {
    console.error('❌ Günlük dağıtım hatası:', err);
  }
}

module.exports = dailyDeliveryCron;
