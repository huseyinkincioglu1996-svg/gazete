const Distributor = require('../models/Distributor');
const Delivery = require('../models/Delivery');
const Payment = require('../models/Payment');

// Her gün 23:59'de çalışır - Günlük ödeme oluştur
async function dailyPaymentCron() {
  try {
    console.log('🔄 Günlük ödeme kontrolü çalışıyor...');

    const today = new Date();
    const gunBaslangic = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const gunBitis = new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1);

    // Günlük ödeme yapan dağıtıcıları bul
    const distributors = await Distributor.find({
      aktif: true,
      odeme_tipi: 'Günlük'
    });

    for (const distributor of distributors) {
      // O günkü tamamlanan dağıtımları bul
      const deliveries = await Delivery.find({
        distributor_id: distributor._id,
        tarih: { $gte: gunBaslangic, $lt: gunBitis },
        durum: 'Tamamlandı'
      });

      if (deliveries.length > 0) {
        const totalGazete = deliveries.reduce((sum, d) => sum + d.gazeteSayisi, 0);
        const tutar = totalGazete * distributor.gazete_fiyat;

        const aciklama = `${totalGazete} gazete × ${distributor.gazete_fiyat}₺ = ${tutar}₺`;

        // Ödeme kaydı oluştur
        const payment = new Payment({
          distributor_id: distributor._id,
          tutar,
          tarih: today,
          donem_baslangic: gunBaslangic,
          donem_bitis: gunBitis,
          aciklama,
          odeme_turu: 'Günlük',
          durum: 'Beklemede'
        });

        await payment.save();
        console.log(`✅ ${distributor.isim} için günlük ödeme oluşturuldu: ${tutar}₺`);
      }
    }

    console.log('✅ Günlük ödeme kontrolü tamamlandı');
  } catch (err) {
    console.error('❌ Günlük ödeme hatası:', err);
  }
}

module.exports = dailyPaymentCron;
