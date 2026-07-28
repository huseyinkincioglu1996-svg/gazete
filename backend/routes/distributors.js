const express = require('express');
const router = express.Router();
const Distributor = require('../models/Distributor');
const { asyncHandler, HttpError } = require('../utils/http');
const { objectId } = require('../utils/validation');
const { buildDistributorPayload } = require('../utils/payloads');

// By default inactive distributors are hidden after a safe delete. Historical
// records remain reachable through reports and `?includeInactive=true`.
router.get('/', asyncHandler(async (req, res) => {
  const filter = req.query.includeInactive === 'true' ? {} : { aktif: true };
  const distributors = await Distributor.find(filter).sort({ isim: 1 });
  res.json(distributors);
}));

router.get('/:id', asyncHandler(async (req, res) => {
  const distributor = await Distributor.findById(objectId(req.params.id));
  if (!distributor) {
    throw new HttpError(404, 'Dağıtıcı bulunamadı');
  }
  res.json(distributor);
}));

router.post('/', asyncHandler(async (req, res) => {
  const distributor = new Distributor(buildDistributorPayload(req.body));
  await distributor.save();
  res.status(201).json(distributor);
}));

router.put('/:id', asyncHandler(async (req, res) => {
  const distributor = await Distributor.findById(objectId(req.params.id));
  if (!distributor) {
    throw new HttpError(404, 'Dağıtıcı bulunamadı');
  }

  Object.assign(distributor, buildDistributorPayload(req.body, { partial: true }));
  await distributor.save();
  res.json(distributor);
}));

// Financial and delivery history must survive a delete request. The distributor
// is therefore deactivated instead of physically removing referenced records.
router.delete('/:id', asyncHandler(async (req, res) => {
  const distributor = await Distributor.findById(objectId(req.params.id));
  if (!distributor) {
    throw new HttpError(404, 'Dağıtıcı bulunamadı');
  }

  if (distributor.aktif) {
    distributor.aktif = false;
    await distributor.save();
  }

  res.json({ mesaj: 'Dağıtıcı pasife alındı; teslimat ve ödeme geçmişi korundu' });
}));

module.exports = router;
