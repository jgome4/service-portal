// Documentación técnica: Jorge Gomez.
// Ejecutar contra una BD de prueba: este smoke crea registros reales y no los elimina.
import assert from 'node:assert/strict';
const base = process.env.PORTAL_URL || 'http://127.0.0.1:5080';
const stamp = Date.now();
const payload = (reference) => ({externalReference:reference,customerId:'SMOKE',serviceCode:'VISIT',description:'Prueba HTTP automatizada'});
async function post(body) { return fetch(`${base}/api/requests`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}); }
const ref = `SMOKE-${stamp}`;
// Los reenvíos simultáneos deben compartir ID y producir una sola respuesta de creación.
const concurrent = await Promise.all(Array.from({length:8},()=>post(payload(ref))));
assert.equal(concurrent.filter(r=>r.status===201).length,1);
assert.ok(concurrent.every(r=>[200,201].includes(r.status)));
const responses = await Promise.all(concurrent.map(r=>r.json()));
assert.equal(new Set(responses.map(r=>r.id)).size,1);
assert.equal((await post({...payload(ref),customerId:'DIFFERENT'})).status,409);
assert.equal((await post({...payload(ref),externalReference:''})).status,400);
assert.equal((await fetch(`${base}/api/requests?status=Unknown`)).status,400);
assert.equal((await fetch(`${base}/api/requests?limit=101`)).status,400);
assert.equal((await fetch(`${base}/api/requests/nonexistent`)).status,404);
assert.equal((await fetch(`${base}/api/requests`,{method:'POST',headers:{'Content-Type':'application/json'},body:'broken'})).status,400);
assert.equal((await post({...payload(ref),description:'a'.repeat(17000)})).status,413);
const retry = await (await post(payload(`RETRY-${stamp}`))).json();
const fail = await (await post(payload(`FAIL-${stamp}`))).json();
async function waitFor(id,status) {
  for (let i=0;i<80;i++) {
    const r=await (await fetch(`${base}/api/requests/${id}`)).json();
    if(r.status===status) return r;
    await new Promise(resolve=>setTimeout(resolve,250));
  }
  throw new Error(`Request ${id} did not reach ${status}`);
}
await waitFor(responses[0].id,'Completed');
assert.equal((await waitFor(retry.id,'Completed')).attempts,3);
assert.equal((await waitFor(fail.id,'Failed')).attempts,3);
const filtered=await (await fetch(`${base}/api/requests?status=Completed&limit=100`)).json();
assert.ok(filtered.every(r=>r.status==='Completed'));
const html=await fetch(base); assert.equal(html.status,200); assert.ok((await html.text()).includes('id="root"'));
console.log('PASS: concurrent idempotency, conflicts, validation, HTTP errors, retries, terminal failure, filtering and static frontend.');
