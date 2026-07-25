const Distributor = require('../models/Distributor');
const Delivery = require('../models/Delivery');
const Payment = require('../models/Payment');

// Her gün 23:59'de çalışır - Aylık ödeme oluştur
async function monthlyPaymentCron() {
  try {
    console.log('🔄 Aylık ödeme kontrolü çalışıyor...');

    const today = new Date();
    const dayOfMonth = today.getDate();

    // Aylık ödeme yapan dağıtıcıları bul
    const distributors = await Distributor.find({
      aktif: true,
      odeme_tipi: 'Aylık',
      odeme_gunleri_ay: dayOfMonth
    });

    for (const distributor of distributors) {
      // Bu ayın başlangıcını bul
      const startOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);

      // Sonraki ayın başlangıcını bul (ay sonunu bulmak için)
      const endOfMonth = new Date(today.getFullYear(), today.getMonth() + 1, 1);

      // Bu ayın tamamlanan dağıtımlarını bul
      const deliveries = await Delivery.find({
        distributor_id: distributor._id,
        tarih: { $gte: startOfMonth, $lt: endOfMonth },
        durum: 'Tamamlandı'
      });

      if (deliveries.length > 0) {
        const totalGazete = deliveries.reduce((sum, d) => sum + d.gazeteSayisi, 0);
        const tutar = totalGazete * distributor.gazete_fiyat;

        const aciklama = `Aylık: ${totalGazete} gazete × ${distributor.gazete_fiyat}₺ = ${tutar}₺`;

        // Ödeme kaydı oluştur
        const payment = new Payment({
          distributor_id: distributor._id,
          tutar,
          tarih: today,
          donem_baslangic: startOfMonth,
          donem_bitis: endOfMonth,
          aciklama,
          odeme_turu: 'Aylık',
          durum: 'Beklemede'
        });

        await payment.save();
        console.log(`✅ ${distributor.isim} için aylık ödeme oluşturuldu: ${tutar}₺`);
      }
    }

    console.log('✅ Aylık ödeme kontrolü tamamlandı');
  } catch (err) {
    console.error('❌ Aylık ödeme hatası:', err);
  }
}

module.exports = monthlyPaymentCron;
