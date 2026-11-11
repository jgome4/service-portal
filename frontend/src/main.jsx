// Documentación técnica: Jorge Gomez.
import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './style.css';

const labels = {Pending:'Pendiente',Processing:'En proceso',Completed:'Completada',Failed:'Fallida'};
const empty = {externalReference:'',customerId:'',serviceCode:'',description:''};
async function api(url, options) {
  const response = await fetch(url, options);
  // Conserva un error comprensible aunque el servidor devuelva una respuesta sin JSON válido.
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.detail || Object.values(body.errors || {}).flat().join(' ') || body.title || 'No fue posible completar la operación.');
  return body;
}
function App() {
  const [form,setForm] = useState(empty), [items,setItems] = useState([]), [status,setStatus] = useState('');
  const [error,setError] = useState(''), [loadError,setLoadError] = useState(''), [message,setMessage] = useState('');
  const [busy,setBusy] = useState(false), [updated,setUpdated] = useState(null), [offset,setOffset] = useState(0);
  const [refresh,setRefresh] = useState(0);
  useEffect(() => {
    let active = true; const controller = new AbortController();
    const load = async () => {
      try {
        const result = await api(`/api/requests?limit=20&offset=${offset}${status ? `&status=${status}` : ''}`,{signal:controller.signal});
        if (active) { setItems(result); setUpdated(new Date()); setLoadError(''); }
      } catch(e) { if (active) setLoadError(e.message); }
    };
    load(); const timer = setInterval(load,2500);
    // Al cambiar filtro o página, cancela consultas anteriores y descarta sus resultados tardíos.
    return () => { active=false; controller.abort(); clearInterval(timer); };
  },[status,offset,refresh]);
  async function submit(event) {
    event.preventDefault(); setBusy(true); setError(''); setMessage('');
    try {
      const created = await api('/api/requests',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(form)});
      setMessage(`Solicitud ${created.externalReference} registrada. Estado: ${labels[created.status]}.`);
      // Solo el envío confirmado limpia el formulario; ante errores se conserva para reintentar.
      setForm(empty); setStatus(''); setOffset(0); setRefresh(x=>x+1);
    } catch(e) { setError(e.message); } finally { setBusy(false); }
  }
  return <div className="layout">
    <aside><a className="brand" href="/">nexo<span>●</span></a><div className="workspace">OPERACIÓN EN CAMPO</div><div className="nav">▤ &nbsp; Solicitudes</div><div className="aside-bottom">Portal interno<br/><span>Gestión de servicios · v1.0</span></div></aside>
    <main><header><span>Espacio de trabajo / <b>Solicitudes</b></span><span className="environment">Entorno de demostración</span></header>
      <section className="heading"><div><div className="eyebrow">CENTRO DE OPERACIONES</div><h1>Cada solicitud, bajo control.</h1><p>Registra un servicio y sigue su avance en un solo lugar.</p></div><span className="live"><i/>Actualización automática</span></section>
      <div className="content"><section className="panel create"><div className="panel-title"><span className="icon">＋</span><div><h2>Nueva solicitud</h2><p>Cuéntanos qué servicio necesitas.</p></div></div>
        <form onSubmit={submit}>
          {[['externalReference','Referencia externa','Ej. ORDEN-2026-001',100],['customerId','Identificación del cliente','Ej. CLIENTE-1024',100],['serviceCode','Código de servicio','Ej. MANTENIMIENTO',50]].map(([key,label,placeholder,max])=><label key={key}>{label}<input required maxLength={max} value={form[key]} placeholder={placeholder} onChange={e=>setForm({...form,[key]:e.target.value})}/></label>)}
          <label>Descripción<textarea required maxLength={2000} rows={4} placeholder="Describe el servicio que se debe realizar…" value={form.description} onChange={e=>setForm({...form,description:e.target.value})}/></label>
          <div className="hint">Usa la misma referencia si necesitas reintentar el envío. Conservaremos una sola solicitud.</div>
          {error && <div className="alert error" role="alert">{error}</div>}
          {message && <div className="alert success" role="status">{message}</div>}
          <button className="primary" disabled={busy}>{busy?'Registrando…':'Registrar solicitud'} <span>↗</span></button>
        </form>
      </section><section className="panel list"><div className="list-heading"><div><h2>Seguimiento de solicitudes</h2><p>El estado se actualiza mientras trabajas.</p></div><button className="refresh" aria-label="Actualizar solicitudes" onClick={()=>setRefresh(x=>x+1)}>↻</button></div>
        <div className="filters"><label htmlFor="status">Estado</label><select id="status" value={status} onChange={e=>{setStatus(e.target.value);setOffset(0);}}><option value="">Todos los estados</option>{Object.entries(labels).map(([key,label])=><option key={key} value={key}>{label}</option>)}</select><span>{updated?`Actualizado ${updated.toLocaleTimeString('es-CO')}`:'Cargando…'}</span></div>
        {loadError && <div className="alert error" role="alert">No se pudo actualizar: {loadError}</div>}
        <div className="table-wrap"><table><thead><tr><th>REFERENCIA / CLIENTE</th><th>SERVICIO</th><th>ESTADO</th><th>INTENTOS</th></tr></thead><tbody>{items.map(item=><tr key={item.id}><td><strong>{item.externalReference}</strong><small>{item.customerId}</small></td><td>{item.serviceCode}</td><td><span className={`badge ${item.status}`}>{labels[item.status]}</span>{item.lastError && <small>{item.lastError}</small>}</td><td>{item.attempts}</td></tr>)}</tbody></table></div>
        {!items.length && <div className="empty"><span>▤</span><h3>{status?'No hay solicitudes con este estado':'Tu próxima solicitud empieza aquí'}</h3><p>{status?'Prueba con otro filtro.':'Completa el formulario para registrar el primer servicio.'}</p></div>}
        <div className="pagination"><span>Página {Math.floor(offset/20)+1} · {items.length} solicitudes</span><button disabled={offset===0} onClick={()=>setOffset(x=>Math.max(0,x-20))}>Anterior</button><button disabled={items.length<20} onClick={()=>setOffset(x=>x+20)}>Siguiente</button></div>
      </section></div><footer>Persistencia local · Procesamiento en segundo plano · Referencias únicas</footer>
    </main></div>;
}
createRoot(document.getElementById('root')).render(<App/>);
