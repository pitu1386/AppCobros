-- ==========================================================
-- LIBRETA DE COBROS · Esquema Supabase (instalación desde cero)
-- Ejecutar en: Supabase Dashboard -> SQL Editor -> New query -> Run
--
-- Modelo de acceso: una sola persona. Cualquier usuario logueado
-- (rol authenticated) ve y edita todo; sin login no se ve nada.
-- El bundle es público en GitHub Pages, pero los datos quedan
-- detrás del login gracias a las políticas RLS de abajo.
-- ==========================================================

drop table if exists public.movimientos cascade;
drop table if exists public.papelera cascade;
drop table if exists public.clientes cascade;
drop table if exists public.grupos cascade;
drop table if exists public.config cascade;

-- 1. CONFIG (fila única id = 'current')
create table public.config (
    id              text primary key default 'current',
    anexo           double precision not null default 8000,
    plantilla       text not null default '',
    plantilla_rec   text not null default '',
    enlace_pago     text not null default '',
    orden           text not null default 'deuda',
    cotizacion_euro double precision not null default 0,
    conceptos_cargo jsonb not null default '[]'::jsonb
);

-- 2. GRUPOS (categorías de cuota)
create table public.grupos (
    id              text primary key,
    nombre          text not null default '',
    cuota           double precision not null default 0,
    historial_cuota jsonb not null default '[]'::jsonb
);

-- 3. CLIENTES
create table public.clientes (
    id          text primary key,
    nombre      text not null default '',
    telefono    text not null default '',
    grupo_id    text,
    anexos      integer not null default 0,
    mes_vencido boolean not null default false,
    archivado   boolean not null default false,
    meses       jsonb not null default '[]'::jsonb,   -- ["YYYY-MM", ...] facturados
    ult_rec     text                                   -- YYYY-MM-DD del último reclamo
);

-- 4. MOVIMIENTOS (cargos y pagos)
create table public.movimientos (
    id              text primary key,
    cliente_id      text not null references public.clientes(id) on delete cascade,
    tipo            text not null,          -- 'cargo' | 'pago'
    fecha           text not null,          -- YYYY-MM-DD
    mes             text,                   -- YYYY-MM (solo cargos mensuales)
    concepto        text not null default '',
    monto           double precision not null default 0,
    cotizacion_euro double precision
);
create index movimientos_cliente_idx on public.movimientos (cliente_id);

-- 5. PAPELERA (movimientos borrados, restaurables un tiempo)
create table public.papelera (
    id             text primary key,        -- = id del movimiento borrado
    cliente_id     text,
    cliente_nombre text not null default '',
    eliminado_el   text not null default '',
    mes_liberado   text,
    movimiento     jsonb not null
);

-- ==========================================================
-- ROW LEVEL SECURITY: logueado = todo, anónimo = nada
-- ==========================================================
alter table public.config      enable row level security;
alter table public.grupos      enable row level security;
alter table public.clientes    enable row level security;
alter table public.movimientos enable row level security;
alter table public.papelera    enable row level security;

create policy "auth_all" on public.config      for all to authenticated using (true) with check (true);
create policy "auth_all" on public.grupos      for all to authenticated using (true) with check (true);
create policy "auth_all" on public.clientes    for all to authenticated using (true) with check (true);
create policy "auth_all" on public.movimientos for all to authenticated using (true) with check (true);
create policy "auth_all" on public.papelera    for all to authenticated using (true) with check (true);

-- ==========================================================
-- REALTIME: para que PC y celular se sincronicen solos
-- ==========================================================
alter publication supabase_realtime add table public.config;
alter publication supabase_realtime add table public.grupos;
alter publication supabase_realtime add table public.clientes;
alter publication supabase_realtime add table public.movimientos;
alter publication supabase_realtime add table public.papelera;

-- ==========================================================
-- FILA DE CONFIG POR DEFECTO
-- ==========================================================
insert into public.config (id) values ('current') on conflict (id) do nothing;

-- ==========================================================
-- FALTA UN PASO MANUAL (una sola vez):
--   Authentication -> Users -> Add user
--     Email: tu email        Password: la que quieras
--     ✔ Auto Confirm User
--   Y en Authentication -> Providers -> Email: desactivar "Confirm email".
--   Con eso ya podés entrar desde la app.
-- ==========================================================
