const test = require('node:test');
const assert = require('node:assert/strict');

const CompanySettings = require('../models/CompanySettings');
const Distributor = require('../models/Distributor');
const {
  MAX_IMAGE_BYTES,
  inspectImageDataUrl
} = require('../utils/imageDataUrl');
const {
  buildCompanySettingsPayload,
  buildDistributorPayload
} = require('../utils/payloads');
const { app } = require('../server');

const DISTRIBUTOR_ID = '507f1f77bcf86cd799439013';
const PNG_DATA_URL = `data:image/png;base64,${Buffer.from([
  137, 80, 78, 71, 13, 10, 26, 10, 0
]).toString('base64')}`;

let server;
let baseUrl;

test.before(async () => {
  await new Promise((resolve) => {
    server = app.listen(0, '127.0.0.1', () => {
      baseUrl = `http://127.0.0.1:${server.address().port}`;
      resolve();
    });
  });
});

test.after(async () => {
  if (server) {
    await new Promise((resolve) => server.close(resolve));
  }
});

async function api(path, options) {
  const response = await fetch(`${baseUrl}${path}`, options);
  const body = await response.json();
  return { response, body };
}

function distributorQuery(result) {
  return {
    select() {
      return this;
    },
    async lean() {
      return result;
    }
  };
}

test('image data URL validation accepts a real signature and rejects spoofed or oversized data', () => {
  assert.deepEqual(inspectImageDataUrl(PNG_DATA_URL), {
    mimeType: 'image/png',
    byteLength: 9
  });

  assert.throws(
    () => inspectImageDataUrl(`data:image/png;base64,${Buffer.from('not-a-png').toString('base64')}`),
    /dosya türüyle uyuşmuyor/
  );

  const oversized = `data:image/png;base64,${Buffer.alloc(MAX_IMAGE_BYTES + 1).toString('base64')}`;
  assert.throws(() => inspectImageDataUrl(oversized), /en fazla 2 MB/);
});

test('branding payloads remain optional and support null for clearing images', () => {
  assert.deepEqual(
    buildDistributorPayload({ profil_gorseli: PNG_DATA_URL }, { partial: true }),
    { profil_gorseli: PNG_DATA_URL }
  );
  assert.deepEqual(
    buildCompanySettingsPayload({
      firma_logosu: null,
      vitrin_dagitici_id: DISTRIBUTOR_ID
    }),
    {
      firma_logosu: null,
      vitrin_dagitici_id: DISTRIBUTOR_ID
    }
  );
  assert.throws(
    () => buildCompanySettingsPayload({ firma_logosu: 'data:image/svg+xml;base64,PHN2Zz4=' }),
    /PNG, JPEG veya WebP/
  );
});

test('company settings GET returns explicit empty branding when no record exists', async (context) => {
  context.mock.method(CompanySettings, 'findOne', async () => null);

  const { response, body } = await api('/api/company-settings');

  assert.equal(response.status, 200);
  assert.deepEqual(body, {
    firma_logosu: null,
    vitrin_dagitici_id: null,
    vitrin_dagitici: null
  });
});

test('company settings GET resolves the selected distributor summary', async (context) => {
  context.mock.method(CompanySettings, 'findOne', async () => ({
    firma_logosu: PNG_DATA_URL,
    vitrin_dagitici_id: DISTRIBUTOR_ID
  }));
  context.mock.method(Distributor, 'findById', () => distributorQuery({
    _id: DISTRIBUTOR_ID,
    isim: 'Merkez Dağıtıcı',
    profil_gorseli: PNG_DATA_URL
  }));

  const { response, body } = await api('/api/company-settings');

  assert.equal(response.status, 200);
  assert.equal(body.vitrin_dagitici_id, DISTRIBUTOR_ID);
  assert.deepEqual(body.vitrin_dagitici, {
    _id: DISTRIBUTOR_ID,
    isim: 'Merkez Dağıtıcı',
    profil_gorseli: PNG_DATA_URL
  });
});

test('company settings PUT validates and persists the selected distributor', async (context) => {
  let receivedFilter;
  let receivedUpdate;
  let receivedOptions;
  context.mock.method(Distributor, 'findById', () => distributorQuery({
    _id: DISTRIBUTOR_ID,
    isim: 'Merkez Dağıtıcı',
    profil_gorseli: PNG_DATA_URL
  }));
  context.mock.method(CompanySettings, 'findOneAndUpdate', async (filter, update, options) => {
    receivedFilter = filter;
    receivedUpdate = update;
    receivedOptions = options;
    return {
      firma_logosu: PNG_DATA_URL,
      vitrin_dagitici_id: DISTRIBUTOR_ID
    };
  });

  const { response, body } = await api('/api/company-settings', {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({
      firma_logosu: PNG_DATA_URL,
      vitrin_dagitici_id: DISTRIBUTOR_ID
    })
  });

  assert.equal(response.status, 200);
  assert.deepEqual(receivedFilter, { singleton_key: 'company' });
  assert.deepEqual(receivedUpdate, {
    $set: {
      firma_logosu: PNG_DATA_URL,
      vitrin_dagitici_id: DISTRIBUTOR_ID
    }
  });
  assert.equal(receivedOptions.upsert, true);
  assert.equal(receivedOptions.runValidators, true);
  assert.equal(body.vitrin_dagitici.isim, 'Merkez Dağıtıcı');
});

test('company settings PUT rejects a missing selected distributor before writing', async (context) => {
  let updateCalls = 0;
  context.mock.method(Distributor, 'findById', () => distributorQuery(null));
  context.mock.method(CompanySettings, 'findOneAndUpdate', async () => {
    updateCalls += 1;
    return null;
  });

  const { response, body } = await api('/api/company-settings', {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ vitrin_dagitici_id: DISTRIBUTOR_ID })
  });

  assert.equal(response.status, 404);
  assert.match(body.hata, /Vitrin dağıtıcısı bulunamadı/);
  assert.equal(updateCalls, 0);
});

test('existing distributor PUT accepts a profile-only update without replacing other fields', async (context) => {
  const distributor = new Distributor({
    _id: DISTRIBUTOR_ID,
    isim: 'Merkez Dağıtıcı',
    adres: 'Merkez',
    telefon: '555 000 00 00',
    bolge: 'Bölge 1'
  });

  context.mock.method(Distributor, 'findById', async () => distributor);
  context.mock.method(Distributor.prototype, 'save', async function saveDistributor() {
    await this.validate();
    return this;
  });

  const { response, body } = await api(`/api/distributors/${DISTRIBUTOR_ID}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ profil_gorseli: PNG_DATA_URL })
  });

  assert.equal(response.status, 200);
  assert.equal(body.isim, 'Merkez Dağıtıcı');
  assert.equal(body.profil_gorseli, PNG_DATA_URL);
});
