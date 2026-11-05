// Builds src/Fakeeh.MedicalKit.Api/Swagger/kit-swagger.json, the content of the "KIT integration (FHIR R4)"
// Swagger document, from the Postman collection the Medical KIT team integrates against.
//
//   node tools/generate-swagger-examples.js [kitCollection] [collectionWithSavedResponses] [environment]
//
// Defaults (relative to the repository root):
//   docs/postman/kit/Fakeeh_MedicalKit_postman_collection_POST_search.json   requests, descriptions, folder order
//   docs/postman/Fakeeh.MedicalKit.FHIR.postman_collection.json              saved responses (matched by request name)
//   docs/postman/Fakeeh.MedicalKit.FHIR.Local.postman_environment.json       values for {{variables}}
//
// Re-run it whenever the KIT collection changes, then rebuild the API.

const fs = require('fs');
const path = require('path');

const repoRoot = path.resolve(__dirname, '..', '..', '..');
const arg = (i, fallback) => path.resolve(process.argv[i] || path.join(repoRoot, fallback));

const kitPath = arg(2, 'docs/postman/kit/Fakeeh_MedicalKit_postman_collection_POST_search.json');
const responsesPath = arg(3, 'docs/postman/Fakeeh.MedicalKit.FHIR.postman_collection.json');
const environmentPath = arg(4, 'docs/postman/Fakeeh.MedicalKit.FHIR.Local.postman_environment.json');
const outputPath = path.resolve(__dirname, '..', 'src', 'Fakeeh.MedicalKit.Api', 'Swagger', 'kit-swagger.json');

const readJson = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^﻿/, ''));
const kit = readJson(kitPath);
const withResponses = readJson(responsesPath);
const environment = readJson(environmentPath);

const samplePatientId = environment.values.find(v => v.key === 'iqama').value;
const sampleBaseUrl = environment.values.find(v => v.key === 'baseUrl').value;

// Saved responses, by request name.
const savedResponses = {};
(function collect(items) {
  for (const item of items) {
    if (item.item) collect(item.item);
    else savedResponses[item.name] = item.response || [];
  }
})(withResponses.item);

const sampleObservationId = (() => {
  const saved = savedResponses['Find one reading by id (_id)'] || [];
  const body = saved.length ? JSON.parse(saved[0].body) : null;
  return body?.entry?.[0]?.resource?.id ?? '00000000-0000-0000-0000-000000000000';
})();

const variables = {
  ...Object.fromEntries(environment.values.filter(v => v.key !== 'apiKey').map(v => [v.key, v.value])),
  iqama: samplePatientId,
  nationalId: '1012345678',
  patientId: samplePatientId,
  observationId: sampleObservationId,
  now: '2026-11-05T10:30:00+03:00',
  runId: 'MHL6Q2ZB'
};

const substitute = (text, baseUrl) =>
  text.replace(/\{\{(\w+)\}\}/g, (match, name) => {
    if (name === 'baseUrl') return baseUrl;
    if (!(name in variables)) throw new Error(`No value for ${match}`);
    return variables[name];
  });

const describe = text => substitute(text || '', '/fhir');

// Operation path as the API exposes it: {{baseUrl}} is /fhir.
const operationPath = raw => {
  const p = substitute(raw.split('?')[0], '/fhir').replace(/\/+$/, '');
  return p || '/fhir';
};

const summaries = {
  'GET /fhir/metadata': 'Server capabilities (CapabilityStatement). Public health check.',
  'POST /fhir/Patient/_search': 'Find a patient in Fakeeh HIS by National ID / Iqama.',
  'GET /fhir/CodeSystem/medical-kit-vital-type': 'Supported vital types, fields, units and LOINC codes.',
  'POST /fhir': 'Send readings as a batch Bundle (recommended).',
  'POST /fhir/Observation': 'Send one reading.',
  'POST /fhir/Observation/_search': "Search a patient's stored readings."
};

const ignoredHeaders = new Set(['accept', 'content-type', 'x-api-key']);

// Large search Bundles are cut to two entries so the Swagger page stays readable.
const trimResponse = body => {
  const replaced = body.split('http://localhost:5008/fhir').join(sampleBaseUrl);
  const json = JSON.parse(replaced);
  if (json.resourceType === 'Bundle' && json.type === 'searchset' && Array.isArray(json.entry) && json.entry.length > 2) {
    json.entry = json.entry.slice(0, 2);
  }
  return json;
};

const operations = [];
const operationByKey = {};
const tags = [];

for (const folder of kit.item) {
  tags.push({ name: folder.name, description: describe(folder.description) });

  for (const request of folder.item) {
    const r = request.request;
    const key = `${r.method} ${operationPath(r.url.raw)}`;
    let operation = operationByKey[key];
    if (!operation) {
      if (!summaries[key]) throw new Error(`No summary for ${key}`);
      operation = operationByKey[key] = {
        key,
        tag: folder.name,
        summary: summaries[key],
        description: describe(folder.description),
        requests: [],
        headers: [],
        responses: []
      };
      operations.push(operation);
    }

    const headers = r.header || [];
    const sendsKey = headers.some(h => h.key.toLowerCase() === 'x-api-key');
    const contentType = headers.find(h => h.key.toLowerCase() === 'content-type')?.value;

    // A request that leaves out the API key only demonstrates the 401; it is not a request example.
    if (sendsKey || key === 'GET /fhir/metadata') {
      let body = null;
      if (r.body?.mode === 'raw') {
        body = JSON.parse(substitute(r.body.raw, '/fhir'));
      } else if (r.body?.mode === 'urlencoded') {
        body = {};
        for (const field of r.body.urlencoded) {
          const value = substitute(field.value, '/fhir');
          if (field.key in body) body[field.key] = [].concat(body[field.key], value);
          else body[field.key] = value;
        }
      }

      if (body !== null) {
        operation.requests.push({ name: request.name, description: describe(r.description), contentType, body });
      }

      for (const header of headers) {
        if (ignoredHeaders.has(header.key.toLowerCase()) || operation.headers.some(h => h.name === header.key)) continue;
        operation.headers.push({ name: header.key, example: substitute(header.value, '/fhir') });
      }
    }

    for (const saved of savedResponses[request.name] || []) {
      if (!saved.body) continue;
      operation.responses.push({
        status: String(saved.code),
        name: request.name,
        description: describe(r.description),
        body: trimResponse(saved.body)
      });
    }
  }
}

const output = {
  generatedFrom: path.basename(kitPath),
  title: kit.info.name,
  description: describe(kit.info.description),
  tags,
  operations
};

fs.writeFileSync(outputPath, JSON.stringify(output, null, 2) + '\n', 'utf8');
console.log(`Wrote ${path.relative(repoRoot, outputPath)}: ${operations.length} operations, ` +
  `${operations.reduce((n, o) => n + o.requests.length, 0)} request examples, ` +
  `${operations.reduce((n, o) => n + o.responses.length, 0)} response examples.`);
for (const o of operations) {
  console.log(`  ${o.key.padEnd(48)} requests=${o.requests.length} responses=${o.responses.map(r => r.status).join(',')} headers=${o.headers.map(h => h.name).join(',')}`);
}
