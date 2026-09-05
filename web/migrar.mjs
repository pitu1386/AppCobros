// Convierte el JSON de backup de la app MAUI en un SQL de carga para Supabase.
//   node migrar.mjs export-actual.json > migracion-datos.sql
// Después: pegar migracion-datos.sql en el SQL Editor de Supabase y Run.
import { readFileSync } from 'node:fs';

const inPath = process.argv[2] || 'export-actual.json';
const data = JSON.parse(readFileSync(inPath, 'utf8'));

const q = (v) => v === null || v === undefined ? 'null' : `'${String(v).replace(/'/g, "''")}'`;
const n = (v) => v === null || v === undefined ? 'null' : Number(v);
const j = (v) => `'${JSON.stringify(v ?? []).replace(/'/g, "''")}'::jsonb`;
const b = (v) => v ? 'true' : 'false';

const out = [];
out.push('-- Carga de datos migrados desde la app MAUI. Generado por migrar.mjs');
out.push('begin;');

// -- CONFIG
const c = data.config ?? {};
const conceptos = (c.conceptosCargo ?? []).map(x => ({
  nombre: x.nombre ?? '', monto: Number(x.monto ?? 0), es_cuota_mensual: !!x.esCuotaMensual,
}));
out.push(`insert into public.config (id, anexo, plantilla, plantilla_rec, enlace_pago, orden, cotizacion_euro, conceptos_cargo)
values ('current', ${n(c.anexo ?? 8000)}, ${q(c.plantilla ?? '')}, ${q(c.plantillaRec ?? '')}, ${q(c.enlacePago ?? '')}, ${q(c.orden ?? 'deuda')}, ${n(c.cotizacionEuro ?? 0)}, ${j(conceptos)})
on conflict (id) do update set
  anexo = excluded.anexo, plantilla = excluded.plantilla, plantilla_rec = excluded.plantilla_rec,
  enlace_pago = excluded.enlace_pago, orden = excluded.orden, cotizacion_euro = excluded.cotizacion_euro,
  conceptos_cargo = excluded.conceptos_cargo;`);

// -- GRUPOS
for (const g of data.grupos ?? []) {
  const hist = (g.historialCuota ?? []).map(h => ({ fecha: h.fecha, cuota: Number(h.cuota) }));
  out.push(`insert into public.grupos (id, nombre, cuota, historial_cuota) values (${q(String(g.id))}, ${q(g.nombre ?? '')}, ${n(g.cuota ?? 0)}, ${j(hist)});`);
}

// -- CLIENTES
for (const cl of data.clients ?? []) {
  out.push(`insert into public.clientes (id, nombre, telefono, grupo_id, anexos, mes_vencido, archivado, meses, ult_rec) values (${q(String(cl.id))}, ${q(cl.nombre ?? '')}, ${q(cl.telefono ?? '')}, ${q(String(cl.grupoId))}, ${n(cl.anexos ?? 0)}, ${b(cl.mesVencido)}, ${b(cl.archivado)}, ${j(cl.meses ?? [])}, ${q(cl.ultRec ?? null)});`);
}

// -- MOVIMIENTOS
for (const cl of data.clients ?? []) {
  for (const m of cl.movimientos ?? []) {
    out.push(`insert into public.movimientos (id, cliente_id, tipo, fecha, mes, concepto, monto, cotizacion_euro) values (${q(String(m.id))}, ${q(String(cl.id))}, ${q(m.tipo)}, ${q(m.fecha)}, ${m.mes ? q(m.mes) : 'null'}, ${q(m.concepto ?? '')}, ${n(m.monto ?? 0)}, ${m.cotizacionEuro == null ? 'null' : n(m.cotizacionEuro)});`);
  }
}

// -- PAPELERA
for (const p of data.papelera ?? []) {
  const mov = p.movimiento ?? {};
  const movRow = {
    id: String(mov.id ?? ''), cliente_id: String(p.clienteId ?? ''), tipo: mov.tipo, fecha: mov.fecha,
    mes: mov.mes ?? null, concepto: mov.concepto ?? '', monto: Number(mov.monto ?? 0),
    cotizacion_euro: mov.cotizacionEuro ?? null,
  };
  out.push(`insert into public.papelera (id, cliente_id, cliente_nombre, eliminado_el, mes_liberado, movimiento) values (${q(String(mov.id))}, ${q(String(p.clienteId))}, ${q(p.clienteNombre ?? '')}, ${q(p.eliminadoEl ?? '')}, ${p.mesLiberado ? q(p.mesLiberado) : 'null'}, ${j(movRow)});`);
}

out.push('commit;');
const counts = `-- grupos: ${(data.grupos ?? []).length}, clientes: ${(data.clients ?? []).length}, movimientos: ${(data.clients ?? []).reduce((s, c) => s + (c.movimientos ?? []).length, 0)}, papelera: ${(data.papelera ?? []).length}`;
out.push(counts);
console.log(out.join('\n'));
console.error(counts);
