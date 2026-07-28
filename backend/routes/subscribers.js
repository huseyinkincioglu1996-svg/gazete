const express = require('express');
const mongoose = require('mongoose');
const router = express.Router();
const Subscriber = require('../models/Subscriber');
const PaymentPeriod = require('../models/PaymentPeriod');
const Distributor = require('../models/Distributor');
const SubscriberDailyDelivery = require('../models/SubscriberDailyDelivery');
const { asyncHandler, HttpError } = require('../utils/http');
const {
  buildSubscriberPayload,
  buildSubscriberStatusPayload
} = require('../utils/payloads');
const { hasOwn, objectId, rejectUnknownFields } = require('../utils/validation');

function withSession(query, session) {
  return session && typeof query?.session === 'function'
    ? query.session(session)
    : query;
}

async function assertPaymentPeriodExists(paymentPeriodId, session = null) {
  if (paymentPeriodId === null || paymentPeriodId === undefined) {
    return;
  }
  const exists = await withSession(
    PaymentPeriod.exists({ _id: paymentPeriodId }),
    session
  );
  if (!exists) {
    throw new HttpError(404, 'Ödeme periyodu bulunamadı');
  }
}

async function findDistributorOrThrow(distributorId, session = null) {
  if (distributorId === null || distributorId === undefined) {
    return null;
  }
  const query = Distributor.findById(distributorId)
    .select({ _id: 1, isim: 1 });
  const distributor = await withSession(query, session);
  if (!distributor) {
    throw new HttpError(404, 'Dağıtıcı bulunamadı');
  }
  return distributor;
}

function activeFilter(query) {
  rejectUnknownFields(query, ['aktif']);
  if (query.aktif === undefined) {
    return {};
  }
  if (query.aktif === 'true') {
    return { aktif: true };
  }
  if (query.aktif === 'false') {
    return { aktif: false };
  }
  throw new HttpError(400, 'aktif sorgu değeri true veya false olmalıdır');
}

router.get('/', asyncHandler(async (req, res) => {
  const subscribers = await Subscriber.find(activeFilter(req.query))
    .populate('odeme_periyodu_id')
    .populate('distributor_id')
    .sort({ isim: 1 });
  res.json(subscribers);
}));

router.get('/:id', asyncHandler(async (req, res) => {
  const subscriber = await Subscriber.findById(objectId(req.params.id))
    .populate('odeme_periyodu_id')
    .populate('distributor_id');
  if (!subscriber) {
    throw new HttpError(404, 'Abone bulunamadı');
  }
  res.json(subscriber);
}));

router.post('/', asyncHandler(async (req, res) => {
  const payload = buildSubscriberPayload(req.body);
  await Promise.all([
    assertPaymentPeriodExists(payload.odeme_periyodu_id),
    findDistributorOrThrow(payload.distributor_id)
  ]);
  const subscriber = new Subscriber(payload);
  await subscriber.save();
  res.status(201).json(subscriber);
}));

router.put('/:id', asyncHandler(async (req, res) => {
  const subscriberId = objectId(req.params.id);
  const payload = buildSubscriberPayload(req.body, { partial: true });
  let updatedSubscriber;

  await mongoose.connection.transaction(async (session) => {
    const subscriber = await withSession(
      Subscriber.findById(subscriberId),
      session
    );
    if (!subscriber) {
      throw new HttpError(404, 'Abone bulunamadı');
    }

    if (hasOwn(payload, 'odeme_periyodu_id')) {
      await assertPaymentPeriodExists(payload.odeme_periyodu_id, session);
    }
    let assignedDistributor = null;
    if (hasOwn(payload, 'distributor_id')) {
      assignedDistributor = await findDistributorOrThrow(
        payload.distributor_id,
        session
      );
    }
    const firstDistributorAssignment = (
      hasOwn(payload, 'distributor_id') &&
      payload.distributor_id !== null &&
      !subscriber.distributor_id
    );

    Object.assign(subscriber, payload);
    await subscriber.save({ session });

    if (firstDistributorAssignment) {
      await SubscriberDailyDelivery.updateMany(
        {
          subscriber_id: subscriber._id,
          tahsil_edildi: true,
          distributor_id: null
        },
        {
          $set: {
            distributor_id: assignedDistributor._id,
            distributor_adi: assignedDistributor.isim
          }
        },
        {
          runValidators: true,
          session
        }
      );
    }

    updatedSubscriber = subscriber;
  });

  res.json(updatedSubscriber);
}));

router.patch('/:id/status', asyncHandler(async (req, res) => {
  const subscriber = await Subscriber.findById(objectId(req.params.id));
  if (!subscriber) {
    throw new HttpError(404, 'Abone bulunamadı');
  }

  Object.assign(subscriber, buildSubscriberStatusPayload(req.body));
  await subscriber.save();
  res.json(subscriber);
}));

module.exports = router;
