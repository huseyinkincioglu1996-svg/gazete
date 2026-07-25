const Distributor = require('../models/Distributor');
const Delivery = require('../models/Delivery');
const Payment = require('../models/Payment');

// Her gün 23:59'de çalışır - Haftalık ödeme oluştur
async function weeklyPaymentCron() {
  try {
    console.log('🔄 Haftalık ödeme kontrolü çalışıyor...');

    const today = new Date();
    const dayOfWeek = today.getDay();

    // Haftalık ödeme yapan dağıtıcıları bul
    const distributors = await Distributor.find({
      aktif: true,
      odeme_tipi: 'Haftalık',
      odeme_gunleri_hafta: dayOfWeek
    });

    for (const distributor of distributors) {
      // Bu haftanın başlangıcını bul (Pazartesi)
      const startOfWeek = new Date(today);
      startOfWeek.setDate(today.getDate() - today.getDay());
      startOfWeek.setHours(0, 0, 0, 0);

      // Hafta sonunu bul
      const endOfWeek = new Date(startOfWeek);
      endOfWeek.setDate(startOfWeek.getDate() + 7);

      // Bu haftanın tamamlanan dağıtımlarını bul
      const deliveries = await Delivery.find({
        distributor_id: distributor._id,
        tarih: { $gte: startOfWeek, $lt: endOfWeek },
        durum: 'Tamamlandı'
      });

      if (deliveries.length > 0) {
        const totalGazete = deliveries.reduce((sum, d) => sum + d.gazeteSayisi, 0);
        const tutar = totalGazete * distributor.gazete_fiyat;

        const aciklama = `Haftalık: ${totalGazete} gazete × ${distributor.gazete_fiyat}₺ = ${tutar}₺`;

        // Ödeme kaydı oluştur
        const payment = new Payment({
          distributor_id: distributor._id,
          tutar,
          tarih: today,
          donem_baslangic: startOfWeek,
          donem_bitis: endOfWeek,
          aciklama,
          odeme_turu: 'Haftalık',
          durum: 'Beklemede'
        });

        await payment.save();
        console.log(`✅ ${distributor.isim} için haftalık ödeme oluşturuldu: ${tutar}₺`);
      }
    }

    console.log('✅ Haftalık ödeme kontrolü tamamlandı');
  } catch (err) {
    console.error('❌ Haftalık ödeme hatası:', err);
  }
}

module.exports = weeklyPaymentCron;
