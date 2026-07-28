const express = require('express');
const router = express.Router();
const CompanySettings = require('../models/CompanySettings');
const Distributor = require('../models/Distributor');
const { asyncHandler, HttpError } = require('../utils/http');
const { buildCompanySettingsPayload } = require('../utils/payloads');

const SETTINGS_FILTER = Object.freeze({ singleton_key: 'company' });

async function distributorSummary(distributorId) {
  if (!distributorId) {
    return null;
  }

  return Distributor.findById(distributorId)
    .select('isim profil_gorseli')
    .lean();
}

async function settingsResponse(settings, knownDistributor) {
  if (!settings) {
    return {
      firma_logosu: null,
      vitrin_dagitici_id: null,
      vitrin_dagitici: null
    };
  }

  const value = typeof settings.toObject === 'function'
    ? settings.toObject()
    : settings;
  const publicValue = { ...value };
  delete publicValue.singleton_key;
  const vitrinDagitici = knownDistributor === undefined
    ? await distributorSummary(publicValue.vitrin_dagitici_id)
    : knownDistributor;

  return {
    ...publicValue,
    vitrin_dagitici: vitrinDagitici || null
  };
}

router.get('/', asyncHandler(async (req, res) => {
  const settings = await CompanySettings.findOne(SETTINGS_FILTER);
  res.json(await settingsResponse(settings));
}));

router.put('/', asyncHandler(async (req, res) => {
  const payload = buildCompanySettingsPayload(req.body);
  let selectedDistributor;

  if (payload.vitrin_dagitici_id) {
    selectedDistributor = await distributorSummary(payload.vitrin_dagitici_id);
    if (!selectedDistributor) {
      throw new HttpError(404, 'Vitrin dağıtıcısı bulunamadı');
    }
  } else if (payload.vitrin_dagitici_id === null) {
    selectedDistributor = null;
  }

  const settings = await CompanySettings.findOneAndUpdate(
    SETTINGS_FILTER,
    { $set: payload },
    {
      new: true,
      upsert: true,
      runValidators: true,
      setDefaultsOnInsert: true
    }
  );
  res.json(await settingsResponse(settings, selectedDistributor));
}));

module.exports = router;
module.exports.settingsResponse = settingsResponse;
