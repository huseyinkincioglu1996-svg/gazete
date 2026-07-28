const Distributor = require('../models/Distributor');
const Delivery = require('../models/Delivery');
const { startOfDay, getTurkishBusinessDay } = require('../utils/date');
const { isDuplicateKeyError } = require('../utils/http');

// Her gün çalışır; iş günü eşlemesi 0=Pazartesi ... 6=Pazar'dır.
async function dailyDeliveryCron(now = new Date()) {
  const today = startOfDay(now);
  const businessDay = getTurkishBusinessDay(today);
  const summary = { created: 0, existing: 0, failed: 0 };

  try {
    const distributors = await Distributor.find({
      aktif: true,
      dagetim_gunleri: businessDay
    });

    for (const distributor of distributors) {
      const timestamp = new Date();
      try {
        const result = await Delivery.updateOne(
          { distributor_id: distributor._id, tarih: today },
          {
            $setOnInsert: {
              distributor_id: distributor._id,
              tarih: today,
              gun: businessDay,
              gazeteSayisi: 0,
              tutar: 0,
              durum: 'Beklemede',
              createdAt: timestamp,
              updatedAt: timestamp
            }
          },
          { upsert: true, timestamps: false }
        );

        if (result.upsertedCount === 1) {
          summary.created += 1;
          console.log(`Dağıtım oluşturuldu: ${distributor.isim}`);
        } else {
          summary.existing += 1;
        }
      } catch (error) {
        if (isDuplicateKeyError(error)) {
          summary.existing += 1;
          continue;
        }

        summary.failed += 1;
        console.error(`Dağıtım oluşturulamadı (${distributor.isim}):`, error.message);
      }
    }
  } catch (error) {
    summary.failed += 1;
    console.error('Günlük dağıtım kontrolü başarısız:', error.message);
  }

  return summary;
}

module.exports = dailyDeliveryCron;
