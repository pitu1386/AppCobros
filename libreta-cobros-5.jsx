import { useState, useEffect, useRef, useMemo } from "react";

/* ============================================================
   COBROS — grupos + detalle de meses adeudados en los mensajes
   + clientes que pagan a mes vencido
   ============================================================ */

const STORAGE_KEY = "cobros-app-v1";

const MESES = [
  "enero","febrero","marzo","abril","mayo","junio",
  "julio","agosto","septiembre","octubre","noviembre","diciembre",
];

const PLANTILLA_DEFAULT =
  "Hola {nombre}! Te confirmo que registramos tu pago de {pago} con fecha {fecha}.\n\n{detalle}\n\nSaldo: {saldo}. ¡Muchas gracias!";

const RECORDATORIO_VIEJO =
  "Hola {nombre}! Te recuerdo que tenés pendiente de pago:\n\n{detalle}\n\nTotal: {saldo}. Cualquier cosa avisame. ¡Gracias!";

const RECORDATORIO_DEFAULT =
  "Hola {nombre}! 👋 ¿Cómo andás? Te paso el resumen de tu cuenta del sistema 🧾\n\n{detalle}\n\n💰 *Total: {saldo}*\n\nCuando puedas coordinamos el pago. ¡Cualquier cosa escribime, gracias! 🙌";

const GRUPOS_DEFAULT = [
  { id: 1, nombre: "1 negocio", cuota: 32000 },
  { id: 2, nombre: "2 negocios", cuota: 54400 },
  { id: 3, nombre: "3 o más negocios", cuota: 64000 },
];

const DEFAULT_DATA = {
  config: {
    anexo: 8000,
    plantilla: PLANTILLA_DEFAULT,
    plantillaRec: RECORDATORIO_DEFAULT,
    orden: "deuda",
  },
  grupos: GRUPOS_DEFAULT,
  clients: [],
  nextId: 1,
  nextGid: 4,
};

/* ---------- helpers ---------- */

const fmt = (n) => "$ " + Math.round(n).toLocaleString("es-AR");

const hoyISO = () => {
  const d = new Date();
  const p = (x) => String(x).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
};

const fechaCorta = (iso) => {
  if (!iso) return "";
  const [y, m, d] = iso.split("-");
  return `${d}/${m}/${y.slice(2)}`;
};

const mesKey = (date = new Date()) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;

const mesLabel = (key) => {
  const [y, m] = key.split("-");
  return `${MESES[parseInt(m, 10) - 1]} ${y}`;
};

const saldoDe = (c) =>
  (c.movimientos || []).reduce(
    (acc, m) => acc + (m.tipo === "cargo" ? m.monto : -m.monto),
    0
  );

const grupoDe = (c, grupos) => grupos.find((g) => g.id === c.grupoId) || null;

const cuotaDe = (c, grupos, cfg) =>
  (grupoDe(c, grupos)?.cuota || 0) + (c.anexos || 0) * cfg.anexo;

const soloDigitos = (s) => (s || "").replace(/\D/g, "");

/* ¿Ya se le cargó la cuota de este mes? Valida tanto la lista de meses
   como los movimientos, para que nunca se duplique. */
const facturadoMes = (c, mes) =>
  (c.meses || []).includes(mes) ||
  (c.movimientos || []).some((m) => m.tipo === "cargo" && m.mes === mes);

/* Cargos impagos: los pagos se imputan a los cargos más viejos primero.
   Devuelve [{...cargo, resto}] con lo que queda sin cubrir de cada uno. */
const pendientesDe = (c) => {
  const movs = c.movimientos || [];
  const cargos = movs
    .filter((m) => m.tipo === "cargo")
    .sort((a, b) => (a.fecha || "").localeCompare(b.fecha || "") || a.id - b.id);
  let pagado = movs
    .filter((m) => m.tipo === "pago")
    .reduce((a, m) => a + m.monto, 0);
  const out = [];
  for (const m of cargos) {
    const cubre = Math.min(pagado, m.monto);
    pagado -= cubre;
    const resto = m.monto - cubre;
    if (resto > 0) out.push({ ...m, resto });
  }
  return out;
};

/* Pendientes "exigibles": si el cliente paga a mes vencido,
   la cuota del mes en curso no se reclama todavía. */
const exigiblesDe = (c, mesActual) =>
  pendientesDe(c).filter((p) => !(c.mesVencido && p.mes === mesActual));

const totalDe = (items) => items.reduce((a, p) => a + p.resto, 0);

const detalleTexto = (items) =>
  items
    .map(
      (p) =>
        `• ${p.concepto}: ${fmt(p.resto)}${p.resto < p.monto ? " (resto)" : ""}`
    )
    .join("\n");

/* intenta reconstruir el mes de cargos viejos "Cuota junio 2026" */
const mesDesdeConcepto = (concepto) => {
  const m = /^Cuota (\p{L}+) (\d{4})$/u.exec(concepto || "");
  if (!m) return undefined;
  const idx = MESES.indexOf(m[1].toLowerCase());
  if (idx < 0) return undefined;
  return `${m[2]}-${String(idx + 1).padStart(2, "0")}`;
};

/* migración de datos viejos */
const migrar = (p) => {
  const d = {
    ...DEFAULT_DATA,
    ...p,
    config: { ...DEFAULT_DATA.config, ...(p.config || {}) },
  };
  if (!d.config.plantillaRec || d.config.plantillaRec === RECORDATORIO_VIEJO)
    d.config.plantillaRec = RECORDATORIO_DEFAULT;
  if (!p.grupos) {
    d.grupos = [
      { id: 1, nombre: "1 negocio", cuota: p.config?.cuota1 ?? 32000 },
      { id: 2, nombre: "2 negocios", cuota: p.config?.cuota2 ?? 54400 },
      { id: 3, nombre: "3 o más negocios", cuota: p.config?.cuota3 ?? 64000 },
    ];
    d.nextGid = 4;
    d.clients = (p.clients || []).map((c) => ({
      ...c,
      grupoId: c.grupoId ?? (c.negocios >= 3 ? 3 : c.negocios === 2 ? 2 : 1),
    }));
  }
  /* completar el mes en cargos de cuota viejos */
  d.clients = (d.clients || []).map((c) => ({
    ...c,
    mesVencido: c.mesVencido || false,
    movimientos: (c.movimientos || []).map((m) =>
      m.tipo === "cargo" && !m.mes
        ? { ...m, mes: mesDesdeConcepto(m.concepto) }
        : m
    ),
  }));
  return d;
};

/* ---------- estilos ---------- */

const CSS = `
@import url('https://fonts.googleapis.com/css2?family=Archivo:wght@400;500;600;700;800&family=Archivo+Expanded:wght@600;700;800&display=swap');

:root{
  --ink:#221C44;
  --ink-2:#3A3370;
  --paper:#F4F3FB;
  --card:#FFFFFF;
  --line:#DEDCEE;
  --money:#0FA36B;
  --money-soft:#DDF5EA;
  --debt:#E14D43;
  --debt-soft:#FBE5E2;
  --muted:#75718F;
  --gold:#FFB547;
}
*{box-sizing:border-box;margin:0;padding:0}
.app{
  font-family:'Archivo',system-ui,sans-serif;
  background:var(--paper);
  color:var(--ink);
  min-height:100vh;
  max-width:520px;
  margin:0 auto;
  padding-bottom:88px;
  font-size:15px;
}
.disp{font-family:'Archivo Expanded','Archivo',sans-serif}
.num{font-variant-numeric:tabular-nums}

.head{
  background:var(--ink);
  color:#F5F3FF;
  padding:18px 18px 20px;
  border-bottom:3px solid var(--gold);
}
.head .brand{
  font-family:'Archivo Expanded',sans-serif;
  font-weight:800;font-size:13px;letter-spacing:.22em;
  text-transform:uppercase;color:var(--gold);
}
.head .sub{font-size:12px;color:#B7B3D9;margin-top:2px}

.kpis{display:flex;gap:10px;margin-top:14px}
.kpi{flex:1;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.12);
  border-radius:10px;padding:10px 12px}
.kpi .lbl{font-size:10px;letter-spacing:.14em;text-transform:uppercase;color:#A5A0CC}
.kpi .val{font-family:'Archivo Expanded',sans-serif;font-weight:700;font-size:17px;margin-top:3px}

.wrap{padding:16px 14px 0}

.card{background:var(--card);border:1px solid var(--line);border-radius:12px;
  padding:14px;margin-bottom:12px}
.card h3{font-size:11px;letter-spacing:.16em;text-transform:uppercase;
  color:var(--muted);font-weight:700;margin-bottom:10px}

.ghead{display:flex;align-items:center;justify-content:space-between;
  margin:18px 2px 8px}
.ghead .gtap{display:flex;align-items:center;gap:8px;background:none;border:none;
  font-family:inherit;text-align:left;cursor:pointer;padding:2px 0;color:inherit}
.ghead .chev{font-size:13px;color:var(--muted);width:14px;flex-shrink:0;
  transition:transform .15s}
.ghead .chev.abierto{transform:rotate(90deg)}
.ghead .gn{font-family:'Archivo Expanded',sans-serif;font-weight:800;
  font-size:13px;letter-spacing:.06em;text-transform:uppercase;color:var(--ink-2)}
.ghead .gc{font-size:12px;color:var(--muted)}
.ghead .add{border:1.5px dashed var(--line);background:none;border-radius:8px;
  padding:5px 10px;font-family:'Archivo',sans-serif;font-size:12px;font-weight:700;
  color:var(--money);cursor:pointer}

.seg{display:flex;background:#fff;border:1.5px solid var(--line);border-radius:9px;
  overflow:hidden;margin-bottom:12px}
.seg button{flex:1;border:none;background:none;padding:9px;
  font-family:'Archivo',sans-serif;font-weight:700;font-size:13px;
  color:var(--muted);cursor:pointer}
.seg button.on{background:var(--ink);color:#F5F3FF}

.btn{display:inline-flex;align-items:center;justify-content:center;gap:8px;
  border:none;border-radius:10px;padding:13px 16px;font-family:'Archivo',sans-serif;
  font-weight:700;font-size:15px;cursor:pointer;width:100%;transition:filter .15s}
.btn:active{filter:brightness(.92)}
.btn-ink{background:var(--ink);color:#F5F3FF}
.btn-money{background:var(--money);color:#fff}
.btn-wa{background:#1FAF59;color:#fff}
.btn-ghost{background:transparent;color:var(--ink-2);border:1.5px solid var(--line)}
.btn-danger-ghost{background:transparent;color:var(--debt);border:1.5px solid var(--debt-soft)}
.btn-sm{padding:9px 12px;font-size:13px;width:auto}

.row{
  display:flex;align-items:center;gap:10px;background:var(--card);
  border:1px solid var(--line);border-radius:12px;padding:12px;margin-bottom:9px;
  cursor:pointer
}
.row .ini{
  width:40px;height:40px;border-radius:9px;background:var(--money-soft);
  color:var(--money);display:flex;align-items:center;justify-content:center;
  font-family:'Archivo Expanded',sans-serif;font-weight:800;font-size:15px;flex-shrink:0
}
.row .ini.deuda{background:var(--debt-soft);color:var(--debt)}
.row .info{flex:1;min-width:0}
.row .nom{font-weight:700;font-size:15px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.row .det{font-size:12px;color:var(--muted);margin-top:2px}
.row .saldo{text-align:right}
.row .saldo .v{font-family:'Archivo Expanded',sans-serif;font-weight:700;font-size:15px}
.row .saldo .t{font-size:10px;letter-spacing:.1em;text-transform:uppercase;color:var(--muted)}
.rojo{color:var(--debt)} .verde{color:var(--money)}

.tagmv{display:inline-block;background:#FFF0D6;color:#9A6A12;border-radius:5px;
  font-size:10px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;
  padding:1px 6px;margin-left:6px;vertical-align:middle}

.sello{
  display:inline-block;border:2px solid currentColor;border-radius:6px;
  padding:3px 10px;font-family:'Archivo Expanded',sans-serif;font-weight:800;
  font-size:11px;letter-spacing:.2em;text-transform:uppercase;
  transform:rotate(-2deg)
}

.mov{display:flex;align-items:center;gap:10px;padding:10px 2px;
  border-bottom:1px dashed var(--line)}
.mov:last-child{border-bottom:none}
.mov .f{font-size:11px;color:var(--muted);width:52px;flex-shrink:0}
.mov .c{flex:1;font-size:13px;min-width:0}
.mov .m{font-weight:700;font-size:14px}
.mov .x{border:none;background:none;color:var(--muted);font-size:15px;
  cursor:pointer;padding:4px}

.pend{display:flex;justify-content:space-between;gap:10px;padding:7px 2px;
  border-bottom:1px dashed var(--line);font-size:13px}
.pend:last-child{border-bottom:none}
.pend .pm{font-weight:700}

.field{margin-bottom:12px}
.field label{display:block;font-size:11px;letter-spacing:.12em;
  text-transform:uppercase;color:var(--muted);font-weight:700;margin-bottom:5px}
.field input,.field select,.field textarea{
  width:100%;border:1.5px solid var(--line);border-radius:9px;padding:11px 12px;
  font-family:'Archivo',sans-serif;font-size:15px;background:#fff;color:var(--ink)}
.field input:focus,.field select:focus,.field textarea:focus{
  outline:2px solid var(--money);outline-offset:1px;border-color:var(--money)}
.field .hint{font-size:11px;color:var(--muted);margin-top:4px}

.check{display:flex;align-items:flex-start;gap:10px;background:#fff;
  border:1.5px solid var(--line);border-radius:9px;padding:11px 12px;
  margin-bottom:12px;cursor:pointer}
.check input{width:18px;height:18px;margin-top:1px;accent-color:var(--money);flex-shrink:0}
.check .ct{font-size:14px;font-weight:600}
.check .cs{font-size:12px;color:var(--muted);margin-top:2px}

.gedit{display:flex;gap:8px;align-items:center;margin-bottom:10px}
.gedit input{border:1.5px solid var(--line);border-radius:9px;padding:10px;
  font-family:'Archivo',sans-serif;font-size:14px;background:#fff;color:var(--ink)}
.gedit .gnom{flex:1.4;min-width:0}
.gedit .gcuo{flex:1;min-width:0}
.gedit .x{border:none;background:var(--debt-soft);color:var(--debt);
  border-radius:8px;width:34px;height:38px;cursor:pointer;font-size:14px;flex-shrink:0}

.veil{position:fixed;inset:0;background:rgba(22,17,53,.6);z-index:40;
  display:flex;align-items:flex-end;justify-content:center}
.sheet{background:var(--paper);width:100%;max-width:520px;max-height:92vh;
  overflow-y:auto;border-radius:18px 18px 0 0;padding:18px 16px 28px;
  animation:up .22s ease}
@keyframes up{from{transform:translateY(40px);opacity:0}to{transform:none;opacity:1}}
@media (prefers-reduced-motion: reduce){.sheet{animation:none}}
.sheet .tit{font-family:'Archivo Expanded',sans-serif;font-weight:800;
  font-size:17px;margin-bottom:14px;display:flex;justify-content:space-between;align-items:center}
.cerrar{border:none;background:var(--line);border-radius:8px;width:30px;height:30px;
  font-size:15px;cursor:pointer;color:var(--ink-2)}

.nav{position:fixed;bottom:0;left:50%;transform:translateX(-50%);
  width:100%;max-width:520px;background:var(--ink);display:flex;z-index:30;
  border-top:3px solid var(--gold)}
.nav button{flex:1;background:none;border:none;color:#8D88B5;padding:11px 0 13px;
  font-family:'Archivo',sans-serif;font-size:11px;font-weight:700;
  letter-spacing:.08em;text-transform:uppercase;cursor:pointer}
.nav button.on{color:var(--gold)}
.nav .ico{font-size:18px;display:block;margin-bottom:3px}
.nav button:focus-visible{outline:2px solid var(--gold);outline-offset:-2px}

.vacio{text-align:center;color:var(--muted);padding:30px 16px;font-size:14px}
.aviso{background:var(--money-soft);border:1px solid #BCE8D4;border-radius:10px;
  padding:11px 12px;font-size:13px;color:var(--ink-2);margin-bottom:12px}
`;

/* ============================================================ */

export default function CobrosApp() {
  const [data, setData] = useState(null);
  const [tab, setTab] = useState("inicio");
  const [selId, setSelId] = useState(null);
  const [editC, setEditC] = useState(null);
  const [pago, setPago] = useState(null);
  const [cargo, setCargo] = useState(null);
  const [wa, setWa] = useState(null);
  const [masivo, setMasivo] = useState(false);
  const [conf, setConf] = useState(null); // {msg, onOk} diálogo de confirmación propio
  const [busca, setBusca] = useState("");
  const [colapsados, setColapsados] = useState({});
  const cargado = useRef(false);

  const toggleGrupo = (key) =>
    setColapsados((p) => ({ ...p, [key]: !p[key] }));

  useEffect(() => {
    (async () => {
      let d = DEFAULT_DATA;
      try {
        const r = await window.storage.get(STORAGE_KEY);
        if (r && r.value) d = migrar(JSON.parse(r.value));
      } catch (e) {
        /* primera vez */
      }
      cargado.current = true;
      setData(d);
    })();
  }, []);

  useEffect(() => {
    if (!data || !cargado.current) return;
    (async () => {
      try {
        await window.storage.set(STORAGE_KEY, JSON.stringify(data));
      } catch (e) {
        console.error("No se pudo guardar", e);
      }
    })();
  }, [data]);

  const cfg = data?.config;
  const grupos = data?.grupos || [];
  const clients = data?.clients || [];
  const sel = clients.find((c) => c.id === selId) || null;
  const mesActual = mesKey();

  const stats = useMemo(() => {
    if (!data) return null;
    let pendiente = 0, cobradoMes = 0;
    for (const c of clients) {
      const s = saldoDe(c);
      if (s > 0) pendiente += s;
      for (const m of c.movimientos || [])
        if (m.tipo === "pago" && m.fecha?.startsWith(mesActual))
          cobradoMes += m.monto;
    }
    return { pendiente, cobradoMes };
  }, [data, clients, mesActual]);

  const sinFacturar = clients.filter((c) => !facturadoMes(c, mesActual));

  /* ---------- mensajes de WhatsApp ---------- */

  const armarMensaje = (plantilla, c, extras = {}) => {
    const items = exigiblesDe(c, mesActual);
    const total = totalDe(items);
    const detalle =
      items.length > 0
        ? "Detalle pendiente:\n" + detalleTexto(items)
        : "No te quedan cuotas pendientes ✅";
    const saldoTxt = total > 0 ? fmt(total) : "$ 0 (cuenta al día ✅)";
    let msg = plantilla
      .replaceAll("{nombre}", c.nombre)
      .replaceAll("{detalle}", detalle)
      .replaceAll("{saldo}", saldoTxt);
    for (const [k, v] of Object.entries(extras))
      msg = msg.replaceAll(`{${k}}`, v);
    return msg;
  };

  /* ---------- acciones ---------- */

  const guardarCliente = (form) => {
    setData((d) => {
      if (form.id) {
        return {
          ...d,
          clients: d.clients.map((c) => (c.id === form.id ? { ...c, ...form } : c)),
        };
      }
      const nuevo = { ...form, id: d.nextId, movimientos: [], meses: [] };
      return { ...d, clients: [...d.clients, nuevo], nextId: d.nextId + 1 };
    });
    setEditC(null);
  };

  const borrarCliente = (id) => {
    setConf({
      msg: "¿Eliminar este cliente y todo su historial? Esta acción no se puede deshacer.",
      onOk: () => {
        setData((d) => ({ ...d, clients: d.clients.filter((c) => c.id !== id) }));
        setSelId(null);
        setEditC(null);
      },
    });
  };

  const generarCargosMes = () => {
    if (sinFacturar.length === 0) return;
    const total = sinFacturar.reduce((a, c) => a + cuotaDe(c, grupos, cfg), 0);
    setConf({
      msg: `Se va a cargar la cuota de ${mesLabel(mesActual)} a ${sinFacturar.length} cliente(s) por un total de ${fmt(total)}. Quien ya la tenga cargada no se duplica. ¿Confirmás?`,
      onOk: () =>
        setData((d) => ({
          ...d,
          clients: d.clients.map((c) => {
            if (facturadoMes(c, mesActual)) return c;
            return {
              ...c,
              meses: [...(c.meses || []), mesActual],
              movimientos: [
                ...(c.movimientos || []),
                {
                  id: Date.now() + c.id,
                  tipo: "cargo",
                  fecha: hoyISO(),
                  mes: mesActual,
                  concepto: `Cuota ${mesLabel(mesActual)}`,
                  monto: cuotaDe(c, d.grupos, d.config),
                },
              ],
            };
          }),
        })),
    });
  };

  const registrarPago = (clientId, monto, fecha) => {
    let cli = null;
    setData((d) => ({
      ...d,
      clients: d.clients.map((c) => {
        if (c.id !== clientId) return c;
        cli = {
          ...c,
          movimientos: [
            ...(c.movimientos || []),
            { id: Date.now(), tipo: "pago", fecha, concepto: "Pago recibido", monto },
          ],
        };
        return cli;
      }),
    }));
    setPago(null);
    setTimeout(() => {
      if (!cli) return;
      const msg = armarMensaje(cfg.plantilla || PLANTILLA_DEFAULT, cli, {
        pago: fmt(monto),
        fecha: fechaCorta(fecha),
      });
      setWa({ tel: soloDigitos(cli.telefono), msg, nombre: cli.nombre });
    }, 50);
  };

  const enviarRecordatorio = (c) => {
    const msg = armarMensaje(cfg.plantillaRec || RECORDATORIO_DEFAULT, c);
    setWa({ tel: soloDigitos(c.telefono), msg, nombre: c.nombre });
  };

  const marcarRecordatorio = (clientId) => {
    setData((d) => ({
      ...d,
      clients: d.clients.map((c) =>
        c.id === clientId ? { ...c, ultRec: hoyISO() } : c
      ),
    }));
  };

  const agregarCargo = (clientId, concepto, monto, fecha) => {
    setData((d) => ({
      ...d,
      clients: d.clients.map((c) =>
        c.id === clientId
          ? {
              ...c,
              movimientos: [
                ...(c.movimientos || []),
                { id: Date.now(), tipo: "cargo", fecha, concepto, monto },
              ],
            }
          : c
      ),
    }));
    setCargo(null);
  };

  const borrarMov = (clientId, movId) => {
    setConf({
      msg: "¿Eliminar este movimiento? Si es la cuota de un mes, ese mes queda liberado para poder cargarla de nuevo.",
      onOk: () =>
        setData((d) => ({
          ...d,
          clients: d.clients.map((c) => {
            if (c.id !== clientId) return c;
            const mov = (c.movimientos || []).find((m) => m.id === movId);
            return {
              ...c,
              movimientos: (c.movimientos || []).filter((m) => m.id !== movId),
              meses:
                mov && mov.tipo === "cargo" && mov.mes
                  ? (c.meses || []).filter((x) => x !== mov.mes)
                  : c.meses || [],
            };
          }),
        })),
    });
  };

  /* ---------- render ---------- */

  if (!data)
    return (
      <div className="app" style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh" }}>
        <style>{CSS}</style>
        <div className="vacio">Cargando tu libreta…</div>
      </div>
    );

  const coincide = (c) => c.nombre.toLowerCase().includes(busca.trim().toLowerCase());

  const orden = cfg.orden || "deuda";
  const ordenar = (arr) =>
    [...arr].sort((a, b) =>
      orden === "nombre"
        ? a.nombre.localeCompare(b.nombre, "es", { sensitivity: "base" })
        : saldoDe(b) - saldoDe(a)
    );
  const setOrden = (o) =>
    setData((d) => ({ ...d, config: { ...d.config, orden: o } }));

  return (
    <div className="app">
      <style>{CSS}</style>

      <header className="head">
        <div className="brand">Libreta de cobros</div>
        <div className="sub">{mesLabel(mesActual)} · {clients.length} cliente{clients.length !== 1 ? "s" : ""} · {grupos.length} grupo{grupos.length !== 1 ? "s" : ""}</div>
        <div className="kpis num">
          <div className="kpi">
            <div className="lbl">Por cobrar</div>
            <div className="val" style={{ color: stats.pendiente > 0 ? "#FFB0A5" : "#8FE6BE" }}>
              {fmt(stats.pendiente)}
            </div>
          </div>
          <div className="kpi">
            <div className="lbl">Cobrado este mes</div>
            <div className="val" style={{ color: "#8FE6BE" }}>{fmt(stats.cobradoMes)}</div>
          </div>
        </div>
      </header>

      <main className="wrap">
        {/* ============ INICIO ============ */}
        {tab === "inicio" && (
          <>
            <div className="card">
              <h3>Cuotas de {mesLabel(mesActual)}</h3>
              {clients.length === 0 ? (
                <div className="vacio">
                  Todavía no hay clientes.<br />Agregalos desde la pestaña <b>Clientes</b>.
                </div>
              ) : sinFacturar.length > 0 ? (
                <>
                  <p style={{ fontSize: 13, color: "var(--muted)", marginBottom: 10 }}>
                    {sinFacturar.length} cliente{sinFacturar.length !== 1 ? "s" : ""} sin la cuota de este mes cargada.
                  </p>
                  <button className="btn btn-ink" onClick={generarCargosMes}>
                    Cargar cuotas del mes a todos
                  </button>
                </>
              ) : (
                <div className="aviso">✓ Todas las cuotas de {mesLabel(mesActual)} ya están cargadas.</div>
              )}
            </div>

            <div className="card">
              <h3>Cuentas con deuda</h3>
              {clients.filter((c) => saldoDe(c) > 0).length === 0 ? (
                <div className="vacio">Nadie debe nada. 🎉</div>
              ) : (
                <>
                  {clients.some((c) => totalDe(exigiblesDe(c, mesActual)) > 0) && (
                    <button
                      className="btn btn-wa"
                      style={{ marginBottom: 12 }}
                      onClick={() => setMasivo(true)}
                    >
                      📲 Reclamar a todos por WhatsApp
                    </button>
                  )}
                  {ordenar(clients.filter((c) => saldoDe(c) > 0)).map((c) => (
                    <FilaCliente key={c.id} c={c} grupos={grupos} cfg={cfg} mesActual={mesActual} onClick={() => setSelId(c.id)} />
                  ))}
                </>
              )}
            </div>
          </>
        )}

        {/* ============ CLIENTES ============ */}
        {tab === "clientes" && (
          <>
            <div className="field">
              <input
                placeholder="Buscar cliente…"
                value={busca}
                onChange={(e) => setBusca(e.target.value)}
              />
            </div>

            <div className="seg">
              <button className={orden === "deuda" ? "on" : ""} onClick={() => setOrden("deuda")}>
                Ordenar por deuda
              </button>
              <button className={orden === "nombre" ? "on" : ""} onClick={() => setOrden("nombre")}>
                Ordenar por nombre
              </button>
            </div>

            {grupos.map((g) => {
              const todos = clients.filter((c) => c.grupoId === g.id);
              const lista = ordenar(todos.filter(coincide));
              const cerrado = !!colapsados[g.id] && !busca;
              return (
                <div key={g.id}>
                  <div className="ghead">
                    <button className="gtap" onClick={() => toggleGrupo(g.id)}>
                      <span className={"chev" + (cerrado ? "" : " abierto")} aria-hidden="true">▶</span>
                      <span>
                        <span className="gn">{g.nombre}</span>
                        <span className="gc num" style={{ display: "block" }}>
                          Cuota {fmt(g.cuota)} · {todos.length} cliente{todos.length !== 1 ? "s" : ""}
                        </span>
                      </span>
                    </button>
                    <button className="add" onClick={() => setEditC({ nuevo: true, grupoId: g.id })}>
                      ＋ Agregar acá
                    </button>
                  </div>
                  {!cerrado &&
                    (lista.length === 0 ? (
                      <div className="vacio" style={{ padding: "10px 0 4px" }}>
                        {busca ? "Sin resultados en este grupo." : "Grupo vacío."}
                      </div>
                    ) : (
                      lista.map((c) => (
                        <FilaCliente key={c.id} c={c} grupos={grupos} cfg={cfg} mesActual={mesActual} onClick={() => setSelId(c.id)} />
                      ))
                    ))}
                </div>
              );
            })}

            {clients.some((c) => !grupoDe(c, grupos)) && (
              <div>
                <div className="ghead">
                  <button className="gtap" onClick={() => toggleGrupo("sg")}>
                    <span className={"chev" + (colapsados["sg"] && !busca ? "" : " abierto")} aria-hidden="true">▶</span>
                    <span className="gn">Sin grupo</span>
                  </button>
                </div>
                {!(colapsados["sg"] && !busca) &&
                  ordenar(clients.filter((c) => !grupoDe(c, grupos) && coincide(c))).map((c) => (
                    <FilaCliente key={c.id} c={c} grupos={grupos} cfg={cfg} mesActual={mesActual} onClick={() => setSelId(c.id)} />
                  ))}
              </div>
            )}

            <div style={{ fontSize: 12, color: "var(--muted)", textAlign: "center", margin: "16px 0 4px" }}>
              Los grupos y sus cuotas se administran en <b>Ajustes</b>.
            </div>
          </>
        )}

        {/* ============ AJUSTES ============ */}
        {tab === "ajustes" && (
          <Ajustes
            cfg={cfg}
            grupos={grupos}
            clients={clients}
            onSave={(ncfg, ngrupos, nextGid) =>
              setData((d) => ({ ...d, config: ncfg, grupos: ngrupos, nextGid }))
            }
            nextGid={data.nextGid || 100}
            pedir={(msg, onOk) => setConf({ msg, onOk })}
            dataJson={JSON.stringify(data)}
            onImport={(p) => {
              setData(migrar(p));
              setTab("clientes");
            }}
          />
        )}
      </main>

      <nav className="nav">
        {[
          ["inicio", "📒", "Inicio"],
          ["clientes", "👥", "Clientes"],
          ["ajustes", "⚙️", "Ajustes"],
        ].map(([k, ico, lbl]) => (
          <button key={k} className={tab === k ? "on" : ""} onClick={() => { setTab(k); setBusca(""); }}>
            <span className="ico" aria-hidden="true">{ico}</span>
            {lbl}
          </button>
        ))}
      </nav>

      {/* ============ HOJAS ============ */}

      {sel && !pago && !cargo && !editC && (
        <Hoja titulo={sel.nombre} onClose={() => setSelId(null)}>
          <DetalleCliente
            c={sel}
            grupos={grupos}
            cfg={cfg}
            mesActual={mesActual}
            onPagar={() => setPago({ clientId: sel.id })}
            onCargo={() => setCargo({ clientId: sel.id })}
            onEditar={() => setEditC(sel)}
            onRecordatorio={() => enviarRecordatorio(sel)}
            onBorrarMov={(movId) => borrarMov(sel.id, movId)}
          />
        </Hoja>
      )}

      {editC && (
        <Hoja
          titulo={editC.nuevo ? "Nuevo cliente" : "Editar cliente"}
          onClose={() => setEditC(null)}
        >
          <FormCliente
            inicial={editC.nuevo ? null : editC}
            preGrupo={editC.nuevo ? editC.grupoId : null}
            grupos={grupos}
            cfg={cfg}
            onSave={guardarCliente}
            onDelete={!editC.nuevo ? () => borrarCliente(editC.id) : null}
          />
        </Hoja>
      )}

      {pago && (
        <Hoja titulo="Registrar cobro" onClose={() => setPago(null)}>
          <FormPago
            c={clients.find((c) => c.id === pago.clientId)}
            mesActual={mesActual}
            onSave={(monto, fecha) => registrarPago(pago.clientId, monto, fecha)}
          />
        </Hoja>
      )}

      {cargo && (
        <Hoja titulo="Agregar cargo" onClose={() => setCargo(null)}>
          <FormCargo
            onSave={(concepto, monto, fecha) =>
              agregarCargo(cargo.clientId, concepto, monto, fecha)
            }
          />
        </Hoja>
      )}

      {masivo && (
        <Hoja titulo="Reclamar deudas" onClose={() => setMasivo(false)}>
          <p style={{ fontSize: 13, color: "var(--muted)", marginBottom: 12 }}>
            Un toque por cliente: se abre WhatsApp con su mensaje personalizado,
            lo enviás y volvés acá. Los enviados quedan marcados con ✓.
          </p>
          {clients
            .map((c) => ({ c, total: totalDe(exigiblesDe(c, mesActual)) }))
            .filter(({ total }) => total > 0)
            .sort((a, b) => b.total - a.total)
            .map(({ c, total }) => {
              const tel = soloDigitos(c.telefono);
              const enviado = c.ultRec === hoyISO();
              const msg = armarMensaje(cfg.plantillaRec || RECORDATORIO_DEFAULT, c);
              return (
                <div className="row" key={c.id} style={{ cursor: "default" }}>
                  <div className="info">
                    <div className="nom">{c.nombre}</div>
                    <div className="det num">Debe {fmt(total)}{enviado ? " · ✓ avisado hoy" : ""}</div>
                  </div>
                  {tel ? (
                    <a
                      href={`https://wa.me/${tel}?text=${encodeURIComponent(msg)}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      style={{ textDecoration: "none" }}
                      onClick={() => marcarRecordatorio(c.id)}
                    >
                      <button className={"btn btn-sm " + (enviado ? "btn-ghost" : "btn-wa")}>
                        {enviado ? "Reenviar" : "Enviar"}
                      </button>
                    </a>
                  ) : (
                    <span style={{ fontSize: 12, color: "var(--muted)" }}>sin teléfono</span>
                  )}
                </div>
              );
            })}
          <button className="btn btn-ghost" style={{ marginTop: 10 }} onClick={() => setMasivo(false)}>
            Listo, cerrar
          </button>
        </Hoja>
      )}

      {wa && (
        <Hoja titulo="Avisar por WhatsApp" onClose={() => setWa(null)}>
          <div className="card" style={{ whiteSpace: "pre-wrap", fontSize: 14 }}>{wa.msg}</div>
          {wa.tel ? (
            <a
              href={`https://wa.me/${wa.tel}?text=${encodeURIComponent(wa.msg)}`}
              target="_blank"
              rel="noopener noreferrer"
              style={{ textDecoration: "none" }}
            >
              <button className="btn btn-wa">Abrir WhatsApp y enviar a {wa.nombre}</button>
            </a>
          ) : (
            <div className="aviso">
              Este cliente no tiene teléfono cargado. Editalo para poder enviarle el aviso.
            </div>
          )}
          <button className="btn btn-ghost" style={{ marginTop: 10 }} onClick={() => setWa(null)}>
            Listo, cerrar
          </button>
        </Hoja>
      )}
      {conf && (
        <div className="veil" style={{ alignItems: "center", padding: 16 }}>
          <div className="card" style={{ width: "100%", maxWidth: 420, marginBottom: 0 }}>
            <p style={{ fontSize: 14, lineHeight: 1.45, marginBottom: 14 }}>{conf.msg}</p>
            <div style={{ display: "flex", gap: 8 }}>
              <button className="btn btn-ghost btn-sm" style={{ flex: 1 }} onClick={() => setConf(null)}>
                Cancelar
              </button>
              <button
                className="btn btn-ink btn-sm"
                style={{ flex: 1 }}
                onClick={() => {
                  const f = conf.onOk;
                  setConf(null);
                  f();
                }}
              >
                Confirmar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function FilaCliente({ c, grupos, cfg, mesActual, onClick }) {
  const s = saldoDe(c);
  const exig = totalDe(exigiblesDe(c, mesActual));
  const g = grupoDe(c, grupos);
  const ini = c.nombre.split(" ").slice(0, 2).map((w) => w[0]).join("").toUpperCase();
  return (
    <div className="row" onClick={onClick}>
      <div className={"ini" + (exig > 0 ? " deuda" : "")}>{ini}</div>
      <div className="info">
        <div className="nom">
          {c.nombre}
          {c.mesVencido && <span className="tagmv">mes vencido</span>}
        </div>
        <div className="det">
          {g ? g.nombre : "sin grupo"}
          {c.anexos > 0 ? ` · ${c.anexos} anexo${c.anexos > 1 ? "s" : ""}` : ""}
          {" · cuota " + fmt(cuotaDe(c, grupos, cfg))}
        </div>
      </div>
      <div className="saldo num">
        <div className={"v " + (s > 0 ? "rojo" : "verde")}>{fmt(Math.abs(s))}</div>
        <div className="t">
          {s > 0 ? (exig > 0 ? "debe" : "mes en curso") : s < 0 ? "a favor" : "al día"}
        </div>
      </div>
    </div>
  );
}

function DetalleCliente({ c, grupos, cfg, mesActual, onPagar, onCargo, onEditar, onRecordatorio, onBorrarMov }) {
  const s = saldoDe(c);
  const g = grupoDe(c, grupos);
  const pend = pendientesDe(c);
  const exig = exigiblesDe(c, mesActual);
  const totalExig = totalDe(exig);
  const movs = [...(c.movimientos || [])].sort(
    (a, b) => (b.fecha || "").localeCompare(a.fecha || "") || b.id - a.id
  );
  return (
    <>
      <div className="card" style={{ textAlign: "center", padding: "20px 14px" }}>
        <div style={{ fontSize: 11, letterSpacing: ".16em", textTransform: "uppercase", color: "var(--muted)", fontWeight: 700 }}>
          Saldo de la cuenta
        </div>
        <div
          className="disp num"
          style={{ fontSize: 36, fontWeight: 800, margin: "6px 0 10px", color: s > 0 ? "var(--debt)" : "var(--money)" }}
        >
          {fmt(Math.abs(s))}
        </div>
        <span className="sello" style={{ color: totalExig > 0 ? "var(--debt)" : "var(--money)" }}>
          {totalExig > 0 ? "Debe" : s > 0 ? "Mes en curso" : s < 0 ? "A favor" : "Al día"}
        </span>
        <div style={{ fontSize: 12, color: "var(--muted)", marginTop: 12 }}>
          {g ? g.nombre : "Sin grupo"} · cuota <b className="num">{fmt(cuotaDe(c, grupos, cfg))}</b>
          {c.anexos > 0 ? ` (incluye ${c.anexos} anexo${c.anexos > 1 ? "s" : ""})` : ""}
          {c.mesVencido ? " · paga a mes vencido" : ""}
          {c.telefono ? <> · 📱 {c.telefono}</> : " · sin teléfono"}
        </div>
      </div>

      {pend.length > 0 && (
        <div className="card">
          <h3>Pendiente de pago</h3>
          {pend.map((p) => {
            const enCurso = c.mesVencido && p.mes === mesActual;
            return (
              <div className="pend num" key={p.id} style={enCurso ? { opacity: 0.55 } : undefined}>
                <span>
                  {p.concepto}
                  {p.resto < p.monto ? " (resto)" : ""}
                  {enCurso ? " — no se reclama aún" : ""}
                </span>
                <span className="pm rojo">{fmt(p.resto)}</span>
              </div>
            );
          })}
          <div className="pend num" style={{ borderTop: "1.5px solid var(--line)", borderBottom: "none", marginTop: 4, paddingTop: 9 }}>
            <span style={{ fontWeight: 700 }}>Total a reclamar</span>
            <span className="pm rojo">{fmt(totalExig)}</span>
          </div>
        </div>
      )}

      <div style={{ display: "flex", gap: 8, marginBottom: 12 }}>
        <button className="btn btn-money" onClick={onPagar}>💵 Registrar cobro</button>
      </div>
      {totalExig > 0 && (
        <div style={{ marginBottom: 12 }}>
          <button className="btn btn-wa" onClick={onRecordatorio}>
            📲 Enviar recordatorio de deuda
          </button>
        </div>
      )}
      <div style={{ display: "flex", gap: 8, marginBottom: 12 }}>
        <button className="btn btn-ghost btn-sm" style={{ flex: 1 }} onClick={onCargo}>
          ＋ Cargo manual
        </button>
        <button className="btn btn-ghost btn-sm" style={{ flex: 1 }} onClick={onEditar}>
          ✎ Editar cliente
        </button>
      </div>

      <div className="card">
        <h3>Historial</h3>
        {movs.length === 0 ? (
          <div className="vacio">Sin movimientos todavía.</div>
        ) : (
          movs.map((m) => (
            <div className="mov" key={m.id}>
              <div className="f num">{fechaCorta(m.fecha)}</div>
              <div className="c">{m.concepto}</div>
              <div className={"m num " + (m.tipo === "pago" ? "verde" : "rojo")}>
                {m.tipo === "pago" ? "−" : "+"}{fmt(m.monto)}
              </div>
              <button className="x" aria-label="Eliminar movimiento" onClick={() => onBorrarMov(m.id)}>✕</button>
            </div>
          ))
        )}
      </div>
    </>
  );
}

function FormCliente({ inicial, preGrupo, grupos, cfg, onSave, onDelete }) {
  const [nombre, setNombre] = useState(inicial?.nombre || "");
  const [telefono, setTelefono] = useState(inicial?.telefono || "");
  const [grupoId, setGrupoId] = useState(
    inicial?.grupoId ?? preGrupo ?? (grupos[0]?.id || 0)
  );
  const [anexos, setAnexos] = useState(inicial?.anexos ?? 0);
  const [mesVencido, setMesVencido] = useState(inicial?.mesVencido || false);

  const cuota = cuotaDe({ grupoId: Number(grupoId), anexos: Number(anexos) || 0 }, grupos, cfg);

  return (
    <>
      <div className="field">
        <label>Nombre del cliente</label>
        <input value={nombre} onChange={(e) => setNombre(e.target.value)} placeholder="Ej: Kiosco San Martín" />
      </div>
      <div className="field">
        <label>WhatsApp</label>
        <input
          value={telefono}
          onChange={(e) => setTelefono(e.target.value)}
          inputMode="tel"
          placeholder="Ej: 5493511234567"
        />
        <div className="hint">Con código de país y área, sin + ni espacios (Argentina: 549…).</div>
      </div>
      <div className="field">
        <label>Grupo</label>
        <select value={grupoId} onChange={(e) => setGrupoId(Number(e.target.value))}>
          {grupos.map((g) => (
            <option key={g.id} value={g.id}>
              {g.nombre} — {fmt(g.cuota)}
            </option>
          ))}
        </select>
      </div>
      <div className="field">
        <label>Anexos contratados</label>
        <input type="number" min="0" value={anexos} onChange={(e) => setAnexos(e.target.value)} />
        <div className="hint">Cada anexo suma {fmt(cfg.anexo)} a la cuota. Dejá 0 si no tiene.</div>
      </div>

      <label className="check">
        <input
          type="checkbox"
          checked={mesVencido}
          onChange={(e) => setMesVencido(e.target.checked)}
        />
        <span>
          <span className="ct">Paga a mes vencido</span>
          <span className="cs" style={{ display: "block" }}>
            La cuota del mes en curso no se le reclama ni aparece en los mensajes hasta el mes siguiente.
          </span>
        </span>
      </label>

      <div className="aviso num">
        Cuota mensual resultante: <b>{fmt(cuota)}</b>
      </div>

      <button
        className="btn btn-ink"
        onClick={() => {
          if (!nombre.trim()) return;
          onSave({
            id: inicial?.id,
            nombre: nombre.trim(),
            telefono: telefono.trim(),
            grupoId: Number(grupoId),
            anexos: Number(anexos) || 0,
            mesVencido,
          });
        }}
      >
        Guardar cliente
      </button>
      {onDelete && (
        <button className="btn btn-danger-ghost" style={{ marginTop: 10 }} onClick={onDelete}>
          Eliminar cliente
        </button>
      )}
    </>
  );
}

function FormPago({ c, mesActual, onSave }) {
  const exig = totalDe(exigiblesDe(c, mesActual));
  const s = saldoDe(c);
  const sugerido = exig > 0 ? exig : Math.max(s, 0);
  const [monto, setMonto] = useState(sugerido > 0 ? String(sugerido) : "");
  const [fecha, setFecha] = useState(hoyISO());
  const n = Number(monto);

  return (
    <>
      <div className="aviso num">
        {c.nombre} debe <b>{fmt(exig)}</b>
        {c.mesVencido && s > exig ? ` (más ${fmt(s - exig)} del mes en curso, que aún no se reclama)` : ""}.
      </div>
      <div className="field">
        <label>Monto cobrado</label>
        <input
          type="number"
          inputMode="numeric"
          min="0"
          value={monto}
          onChange={(e) => setMonto(e.target.value)}
          placeholder="0"
        />
        <div className="hint">Puede ser un pago parcial: el resto queda como deuda.</div>
      </div>
      <div className="field">
        <label>Fecha</label>
        <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} />
      </div>
      <button className="btn btn-money" onClick={() => n > 0 && fecha && onSave(n, fecha)}>
        Registrar cobro y avisar por WhatsApp
      </button>
    </>
  );
}

function FormCargo({ onSave }) {
  const [concepto, setConcepto] = useState("");
  const [monto, setMonto] = useState("");
  const [fecha, setFecha] = useState(hoyISO());
  const n = Number(monto);
  return (
    <>
      <div className="field">
        <label>Concepto</label>
        <input
          value={concepto}
          onChange={(e) => setConcepto(e.target.value)}
          placeholder="Ej: Anexo puntual, instalación, ajuste…"
        />
      </div>
      <div className="field">
        <label>Monto</label>
        <input type="number" inputMode="numeric" min="0" value={monto} onChange={(e) => setMonto(e.target.value)} placeholder="0" />
      </div>
      <div className="field">
        <label>Fecha</label>
        <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} />
      </div>
      <button
        className="btn btn-ink"
        onClick={() => concepto.trim() && n > 0 && onSave(concepto.trim(), n, fecha)}
      >
        Agregar cargo
      </button>
    </>
  );
}

function Ajustes({ cfg, grupos, clients, onSave, nextGid, pedir, dataJson, onImport }) {
  const [f, setF] = useState({ ...cfg });
  const [gs, setGs] = useState(grupos.map((g) => ({ ...g })));
  const [gid, setGid] = useState(nextGid);
  const [ok, setOk] = useState(false);
  const [gAviso, setGAviso] = useState("");
  const [bkModo, setBkModo] = useState(null); // 'exp' | 'imp'
  const [bkTexto, setBkTexto] = useState("");
  const [bkAviso, setBkAviso] = useState("");

  const setCfg = (k, v) => { setF((p) => ({ ...p, [k]: v })); setOk(false); };
  const setG = (id, k, v) => {
    setGs((p) => p.map((g) => (g.id === id ? { ...g, [k]: v } : g)));
    setOk(false);
  };
  const addG = () => {
    setGs((p) => [...p, { id: gid, nombre: "Nuevo grupo", cuota: 0 }]);
    setGid(gid + 1);
    setOk(false);
  };
  const delG = (id) => {
    const enUso = clients.filter((c) => c.grupoId === id).length;
    if (enUso > 0) {
      setGAviso(`No se puede eliminar: hay ${enUso} cliente(s) en este grupo. Movelos a otro grupo primero.`);
      return;
    }
    setGAviso("");
    setGs((p) => p.filter((g) => g.id !== id));
    setOk(false);
  };

  return (
    <>
      <div className="card">
        <h3>Grupos y cuotas</h3>
        {gs.map((g) => (
          <div className="gedit" key={g.id}>
            <input
              className="gnom"
              value={g.nombre}
              onChange={(e) => setG(g.id, "nombre", e.target.value)}
              placeholder="Nombre del grupo"
            />
            <input
              className="gcuo num"
              type="number"
              inputMode="numeric"
              min="0"
              value={g.cuota}
              onChange={(e) => setG(g.id, "cuota", Number(e.target.value) || 0)}
              placeholder="Cuota"
            />
            <button className="x" aria-label={"Eliminar grupo " + g.nombre} onClick={() => delG(g.id)}>🗑</button>
          </div>
        ))}
        <button className="btn btn-ghost btn-sm" onClick={addG} style={{ width: "100%" }}>
          ＋ Agregar grupo
        </button>
        {gAviso && <div className="aviso" style={{ marginTop: 10 }}>{gAviso}</div>}
        <div className="hint" style={{ fontSize: 12, color: "var(--muted)", marginTop: 8 }}>
          Cuando aumentes la cuota de un grupo, el cambio aplica a todos sus clientes en las cuotas que cargues de ahí en adelante.
        </div>
      </div>

      <div className="card">
        <h3>Anexos</h3>
        <div className="field">
          <label>Precio por anexo (mensual)</label>
          <input
            type="number"
            inputMode="numeric"
            min="0"
            value={f.anexo}
            onChange={(e) => setCfg("anexo", Number(e.target.value) || 0)}
          />
          <div className="hint">Se suma a la cuota del grupo por cada anexo que tenga el cliente.</div>
        </div>
      </div>

      <div className="card">
        <h3>Mensaje al registrar un cobro</h3>
        <div className="field">
          <textarea
            rows={6}
            value={f.plantilla}
            onChange={(e) => setCfg("plantilla", e.target.value)}
          />
          <div className="hint">
            Podés usar: {"{nombre}"}, {"{pago}"}, {"{fecha}"}, {"{detalle}"} (lista de meses adeudados) y {"{saldo}"} (total pendiente).
          </div>
        </div>
      </div>

      <div className="card">
        <h3>Mensaje de recordatorio de deuda</h3>
        <div className="field">
          <textarea
            rows={5}
            value={f.plantillaRec}
            onChange={(e) => setCfg("plantillaRec", e.target.value)}
          />
          <div className="hint">
            Podés usar: {"{nombre}"}, {"{detalle}"} y {"{saldo}"}.
          </div>
        </div>
      </div>

      <div className="card">
        <h3>Copia de seguridad</h3>
        <p style={{ fontSize: 13, color: "var(--muted)", marginBottom: 10 }}>
          Cada copia de la app guarda sus datos por separado. Usá esto para pasar
          tus clientes y movimientos de una versión a otra.
        </p>
        <div style={{ display: "flex", gap: 8, marginBottom: 10 }}>
          <button
            className="btn btn-ghost btn-sm"
            style={{ flex: 1 }}
            onClick={() => {
              setBkTexto(dataJson);
              setBkModo("exp");
              setBkAviso("");
            }}
          >
            ⬆ Exportar datos
          </button>
          <button
            className="btn btn-ghost btn-sm"
            style={{ flex: 1 }}
            onClick={() => {
              setBkTexto("");
              setBkModo("imp");
              setBkAviso("");
            }}
          >
            ⬇ Importar datos
          </button>
        </div>

        {bkModo === "exp" && (
          <>
            <div className="field">
              <textarea
                rows={4}
                readOnly
                value={bkTexto}
                onFocus={(e) => e.target.select()}
                style={{ fontSize: 11 }}
              />
              <div className="hint">
                Este texto es toda tu libreta. Copialo y pegalo en "Importar datos" de la otra versión.
              </div>
            </div>
            <button
              className="btn btn-ink btn-sm"
              style={{ width: "100%" }}
              onClick={async () => {
                try {
                  await navigator.clipboard.writeText(bkTexto);
                  setBkAviso("✓ Copiado al portapapeles. Ahora pegalo en la otra versión.");
                } catch (e) {
                  setBkAviso("Mantené presionado el texto de arriba, elegí Seleccionar todo y Copiar.");
                }
              }}
            >
              📋 Copiar todo
            </button>
          </>
        )}

        {bkModo === "imp" && (
          <>
            <div className="field">
              <textarea
                rows={4}
                value={bkTexto}
                onChange={(e) => setBkTexto(e.target.value)}
                placeholder="Pegá acá el texto exportado desde la otra versión…"
                style={{ fontSize: 11 }}
              />
            </div>
            <button
              className="btn btn-ink btn-sm"
              style={{ width: "100%" }}
              onClick={() => {
                try {
                  const p = JSON.parse(bkTexto);
                  if (!p || !Array.isArray(p.clients)) throw new Error("formato");
                  pedir(
                    `Se van a importar ${p.clients.length} cliente(s) y se reemplaza todo lo que haya en esta versión. ¿Confirmás?`,
                    () => onImport(p)
                  );
                } catch (e) {
                  setBkAviso("El texto pegado no es válido. Verificá haber copiado todo, desde { hasta }.");
                }
              }}
            >
              ⬇ Importar y reemplazar
            </button>
          </>
        )}

        {bkAviso && (
          <div className="aviso" style={{ marginTop: 10 }}>{bkAviso}</div>
        )}
      </div>

      <button
        className="btn btn-ink"
        onClick={() => { onSave(f, gs, gid); setOk(true); }}
      >
        Guardar ajustes
      </button>
      {ok && <div className="aviso" style={{ marginTop: 10 }}>✓ Ajustes guardados.</div>}
    </>
  );
}

function Hoja({ titulo, onClose, children }) {
  return (
    <div className="veil" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="sheet">
        <div className="tit disp">
          {titulo}
          <button className="cerrar" aria-label="Cerrar" onClick={onClose}>✕</button>
        </div>
        {children}
      </div>
    </div>
  );
}
