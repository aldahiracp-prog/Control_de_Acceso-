--
-- PostgreSQL database dump
--

\restrict mOaQBUJHcqnu9NmQytrcvlL3bMdcLg0Zj9KDCNvFckWQteEqvo5XPFkI0rhlVhh

-- Dumped from database version 18.4
-- Dumped by pg_dump version 18.4

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: public; Type: SCHEMA; Schema: -; Owner: pg_database_owner
--

CREATE SCHEMA public;


ALTER SCHEMA public OWNER TO pg_database_owner;

--
-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: pg_database_owner
--

COMMENT ON SCHEMA public IS 'standard public schema';


--
-- Name: calcular_duracion(); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.calcular_duracion() RETURNS trigger
    LANGUAGE plpgsql
    AS $$

BEGIN

IF NEW.hora_fin IS NOT NULL THEN

NEW.duracion_minutos=

ROUND(

EXTRACT(

EPOCH

FROM

(

NEW.hora_fin

-

NEW.hora_inicio

)

)/60

);

END IF;

RETURN NEW;

END;

$$;


ALTER FUNCTION public.calcular_duracion() OWNER TO postgres;

--
-- Name: calcular_inactividad(); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.calcular_inactividad() RETURNS trigger
    LANGUAGE plpgsql
    AS $$

BEGIN

IF

NEW.hora_fin

IS NOT NULL

THEN

NEW.minutos=

ROUND(

EXTRACT(

EPOCH

FROM

(

NEW.hora_fin

-

NEW.hora_inicio

)

)/60

);

END IF;

RETURN NEW;

END;

$$;


ALTER FUNCTION public.calcular_inactividad() OWNER TO postgres;

--
-- Name: determinar_tipo_uso(); Type: FUNCTION; Schema: public; Owner: postgres
--

CREATE FUNCTION public.determinar_tipo_uso() RETURNS trigger
    LANGUAGE plpgsql
    AS $$

BEGIN

IF

NEW.hora_inicio::TIME

BETWEEN

'08:00:00'

AND

'12:00:00'

THEN

NEW.tipo_uso='CLASE';

ELSE

NEW.tipo_uso='PRESTAMO';

END IF;

RETURN NEW;

END;

$$;


ALTER FUNCTION public.determinar_tipo_uso() OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: carreras; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.carreras (
    id_carrera integer NOT NULL,
    nombre character varying(120) NOT NULL,
    codigo character varying(20) NOT NULL
);


ALTER TABLE public.carreras OWNER TO postgres;

--
-- Name: carreras_id_carrera_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.carreras_id_carrera_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.carreras_id_carrera_seq OWNER TO postgres;

--
-- Name: carreras_id_carrera_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.carreras_id_carrera_seq OWNED BY public.carreras.id_carrera;


--
-- Name: computadoras; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.computadoras (
    id_computadora integer NOT NULL,
    nombre_equipo character varying(100) NOT NULL,
    ip character varying(50) NOT NULL,
    mac character varying(50) NOT NULL,
    sistema_operativo character varying(50) DEFAULT 'Windows'::character varying,
    id_ubicacion integer NOT NULL,
    estado character varying(20) DEFAULT 'ACTIVA'::character varying,
    ultima_conexion timestamp without time zone,
    fecha_registro timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.computadoras OWNER TO postgres;

--
-- Name: computadoras_id_computadora_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.computadoras_id_computadora_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.computadoras_id_computadora_seq OWNER TO postgres;

--
-- Name: computadoras_id_computadora_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.computadoras_id_computadora_seq OWNED BY public.computadoras.id_computadora;


--
-- Name: eventos_sistema; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.eventos_sistema (
    id_evento integer NOT NULL,
    id_sesion integer,
    id_computadora integer,
    tipo_evento character varying(50),
    descripcion text,
    fecha_evento timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    detalles_json text
);


ALTER TABLE public.eventos_sistema OWNER TO postgres;

--
-- Name: eventos_sistema_id_evento_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.eventos_sistema_id_evento_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.eventos_sistema_id_evento_seq OWNER TO postgres;

--
-- Name: eventos_sistema_id_evento_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.eventos_sistema_id_evento_seq OWNED BY public.eventos_sistema.id_evento;


--
-- Name: registro_inactividad; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.registro_inactividad (
    id_inactividad integer NOT NULL,
    id_sesion integer NOT NULL,
    hora_inicio timestamp without time zone,
    hora_fin timestamp without time zone,
    minutos integer
);


ALTER TABLE public.registro_inactividad OWNER TO postgres;

--
-- Name: registro_inactividad_id_inactividad_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.registro_inactividad_id_inactividad_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.registro_inactividad_id_inactividad_seq OWNER TO postgres;

--
-- Name: registro_inactividad_id_inactividad_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.registro_inactividad_id_inactividad_seq OWNED BY public.registro_inactividad.id_inactividad;


--
-- Name: roles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.roles (
    id_rol integer NOT NULL,
    nombre character varying(30) NOT NULL,
    descripcion character varying(250)
);


ALTER TABLE public.roles OWNER TO postgres;

--
-- Name: roles_id_rol_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.roles_id_rol_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.roles_id_rol_seq OWNER TO postgres;

--
-- Name: roles_id_rol_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.roles_id_rol_seq OWNED BY public.roles.id_rol;


--
-- Name: sesiones; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.sesiones (
    id_sesion integer NOT NULL,
    id_usuario integer NOT NULL,
    id_computadora integer NOT NULL,
    fecha date DEFAULT CURRENT_DATE,
    hora_inicio timestamp without time zone NOT NULL,
    hora_fin timestamp without time zone,
    duracion_minutos integer,
    tipo_uso character varying(20),
    cerrado_por character varying(30),
    estado character varying(20) DEFAULT 'ACTIVA'::character varying,
    CONSTRAINT sesiones_cerrado_por_check CHECK (((cerrado_por IS NULL) OR ((cerrado_por)::text = ANY ((ARRAY['LOGOUT'::character varying, 'APAGADO'::character varying, 'INACTIVIDAD'::character varying, 'SUSPENSION'::character varying, 'CIERRE_SESION_WINDOWS'::character varying, 'BLOQUEO_PANTALLA_WINDOWS'::character varying, 'CAMBIO_USUARIO'::character varying, 'CAMBIO_SESION'::character varying, 'CIERRE_DESARROLLO'::character varying])::text[])))),
    CONSTRAINT sesiones_estado_check CHECK (((estado)::text = ANY ((ARRAY['ACTIVA'::character varying, 'FINALIZADA'::character varying, 'BLOQUEADA'::character varying])::text[]))),
    CONSTRAINT sesiones_tipo_uso_check CHECK (((tipo_uso IS NULL) OR ((tipo_uso)::text = ANY ((ARRAY['CLASE'::character varying, 'PRESTAMO'::character varying])::text[]))))
);


ALTER TABLE public.sesiones OWNER TO postgres;

--
-- Name: sesiones_id_sesion_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.sesiones_id_sesion_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.sesiones_id_sesion_seq OWNER TO postgres;

--
-- Name: sesiones_id_sesion_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.sesiones_id_sesion_seq OWNED BY public.sesiones.id_sesion;


--
-- Name: ubicaciones; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.ubicaciones (
    id_ubicacion integer NOT NULL,
    nombre character varying(100) NOT NULL,
    descripcion text,
    activo boolean DEFAULT true,
    fecha_creacion timestamp without time zone DEFAULT now()
);


ALTER TABLE public.ubicaciones OWNER TO postgres;

--
-- Name: ubicaciones_id_ubicacion_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.ubicaciones_id_ubicacion_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.ubicaciones_id_ubicacion_seq OWNER TO postgres;

--
-- Name: ubicaciones_id_ubicacion_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.ubicaciones_id_ubicacion_seq OWNED BY public.ubicaciones.id_ubicacion;


--
-- Name: usuarios; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.usuarios (
    id_usuario integer NOT NULL,
    cedula character(10) NOT NULL,
    nombre character varying(80) NOT NULL,
    apellido character varying(80) NOT NULL,
    correo character varying(120),
    id_rol integer NOT NULL,
    id_carrera integer,
    activo boolean DEFAULT true,
    fecha_creacion timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.usuarios OWNER TO postgres;

--
-- Name: usuarios_id_usuario_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.usuarios_id_usuario_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.usuarios_id_usuario_seq OWNER TO postgres;

--
-- Name: usuarios_id_usuario_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.usuarios_id_usuario_seq OWNED BY public.usuarios.id_usuario;


--
-- Name: vw_reporte_general; Type: VIEW; Schema: public; Owner: postgres
--

CREATE VIEW public.vw_reporte_general AS
 SELECT s.id_sesion,
    u.cedula,
    u.nombre,
    u.apellido,
    r.nombre AS rol,
    ca.nombre AS carrera,
    c.nombre_equipo,
    c.ip,
    c.mac,
    ub.nombre AS ubicacion,
    s.hora_inicio,
    s.hora_fin,
    s.duracion_minutos,
    s.tipo_uso,
    s.cerrado_por,
    s.estado
   FROM (((((public.sesiones s
     JOIN public.usuarios u ON ((s.id_usuario = u.id_usuario)))
     JOIN public.roles r ON ((u.id_rol = r.id_rol)))
     LEFT JOIN public.carreras ca ON ((u.id_carrera = ca.id_carrera)))
     JOIN public.computadoras c ON ((s.id_computadora = c.id_computadora)))
     JOIN public.ubicaciones ub ON ((c.id_ubicacion = ub.id_ubicacion)));


ALTER VIEW public.vw_reporte_general OWNER TO postgres;

--
-- Name: carreras id_carrera; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.carreras ALTER COLUMN id_carrera SET DEFAULT nextval('public.carreras_id_carrera_seq'::regclass);


--
-- Name: computadoras id_computadora; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.computadoras ALTER COLUMN id_computadora SET DEFAULT nextval('public.computadoras_id_computadora_seq'::regclass);


--
-- Name: eventos_sistema id_evento; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.eventos_sistema ALTER COLUMN id_evento SET DEFAULT nextval('public.eventos_sistema_id_evento_seq'::regclass);


--
-- Name: registro_inactividad id_inactividad; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.registro_inactividad ALTER COLUMN id_inactividad SET DEFAULT nextval('public.registro_inactividad_id_inactividad_seq'::regclass);


--
-- Name: roles id_rol; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles ALTER COLUMN id_rol SET DEFAULT nextval('public.roles_id_rol_seq'::regclass);


--
-- Name: sesiones id_sesion; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sesiones ALTER COLUMN id_sesion SET DEFAULT nextval('public.sesiones_id_sesion_seq'::regclass);


--
-- Name: ubicaciones id_ubicacion; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.ubicaciones ALTER COLUMN id_ubicacion SET DEFAULT nextval('public.ubicaciones_id_ubicacion_seq'::regclass);


--
-- Name: usuarios id_usuario; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.usuarios ALTER COLUMN id_usuario SET DEFAULT nextval('public.usuarios_id_usuario_seq'::regclass);


--
-- Data for Name: carreras; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.carreras (id_carrera, nombre, codigo) VALUES (1, 'Desarrollo de Software', 'SW');
INSERT INTO public.carreras (id_carrera, nombre, codigo) VALUES (2, 'Administración', 'ADM');
INSERT INTO public.carreras (id_carrera, nombre, codigo) VALUES (3, 'Protección del Medio Ambiente', 'AMB');


--
-- Data for Name: computadoras; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (2, 'BIB-02', '192.168.1.11', '00:AA:11:BB:02', 'Windows 11', 1, 'ACTIVA', '2026-06-20 23:05:27.241222', '2026-06-20 23:05:27.241222');
INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (3, 'LAB3A-01', '192.168.1.20', '00:AA:11:BB:03', 'Windows 11', 2, 'ACTIVA', '2026-06-20 23:05:27.241222', '2026-06-20 23:05:27.241222');
INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (4, 'LAB3A-02', '192.168.1.21', '00:AA:11:BB:04', 'Windows 11', 2, 'ACTIVA', '2026-06-20 23:05:27.241222', '2026-06-20 23:05:27.241222');
INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (5, 'LAB3B-01', '192.168.1.30', '00:AA:11:BB:05', 'Windows 11', 3, 'ACTIVA', '2026-06-20 23:05:27.241222', '2026-06-20 23:05:27.241222');
INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (6, 'LAB3B-02', '192.168.1.31', '00:AA:11:BB:06', 'Windows 11', 3, 'ACTIVA', '2026-06-20 23:05:27.241222', '2026-06-20 23:05:27.241222');
INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (7, 'LAB3D-01', '192.168.1.40', '00:AA:11:BB:07', 'Windows 11', 4, 'ACTIVA', '2026-06-20 23:05:27.241222', '2026-06-20 23:05:27.241222');
INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (8, 'LAB3D-02', '192.168.1.41', '00:AA:11:BB:08', 'Windows 11', 4, 'ACTIVA', '2026-06-20 23:05:27.241222', '2026-06-20 23:05:27.241222');
INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (1, 'BIB-01', '192.168.1.10', '00:AA:11:BB:01', 'Windows 11', 1, 'ACTIVA', '2026-06-20 23:05:27.241222', '2026-06-20 23:05:27.241222');
INSERT INTO public.computadoras (id_computadora, nombre_equipo, ip, mac, sistema_operativo, id_ubicacion, estado, ultima_conexion, fecha_registro) VALUES (9, 'MAEC', '192.168.100.9', 'A0-AD-9F-1E-40-DB', 'Windows 11', 2, 'ACTIVA', NULL, '2026-06-23 20:46:56.134606');


--
-- Data for Name: eventos_sistema; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (2, 6, 8, 'APAGADO', 'Equipo apagado por usuario', '2026-06-20 23:05:27.241222', NULL);
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (3, 2, 4, 'LOGIN', 'Inicio correcto', '2026-06-20 23:05:27.241222', NULL);
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (4, 3, 3, 'LOGOUT', 'Cierre correcto', '2026-06-20 23:05:27.241222', NULL);
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (6, NULL, NULL, 'LOGIN_FALLIDO', 'Intento de inicio con cédula inválida: 1723456781 - El dígito verificador no coincide. Cédula inválida.', '2026-06-21 19:04:05.406116', '{"cedula":"1723456781","error":"El dígito verificador no coincide. Cédula inválida."}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (8, 10, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 20:51:12.012877', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (9, 11, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 21:39:43.963788', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (10, 12, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 21:47:15.630483', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (1, NULL, 1, 'BLOQUEO', 'Bloqueo automático por inactividad', '2026-06-20 23:05:27.241222', NULL);
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (7, NULL, 1, 'LOGIN', 'Usuario Miguel Perez inició sesión en BIB-01', '2026-06-21 19:48:49.699868', '{"cedula":"1723456781","tipo_uso":"PRESTAMO"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (11, 13, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 22:08:51.477556', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (12, 13, 9, 'LOGOUT', 'Sesión cerrada - Usuario: Miguel Echeverria', '2026-06-23 22:09:04.597675', '{"duracion":0,"motivo":"BLOQUEO_PANTALLA_WINDOWS"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (13, 14, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 22:09:22.400054', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (14, 14, 9, 'LOGOUT', 'Sesión cerrada - Usuario: Miguel Echeverria', '2026-06-23 22:09:27.549471', '{"duracion":0,"motivo":"LOGOUT"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (15, 15, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 22:09:37.194011', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (16, 15, 9, 'BLOQUEO', 'Pantalla bloqueada por inactividad (5 min)', '2026-06-23 22:16:44.165356', '{"minutos":5}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (17, 16, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 23:04:34.464289', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (18, 16, 9, 'LOGOUT', 'Sesión cerrada - Usuario: Miguel Echeverria', '2026-06-23 23:04:43.124933', '{"duracion":0,"motivo":"BLOQUEO_PANTALLA_WINDOWS"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (19, 17, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 23:04:54.564867', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (20, 17, 9, 'LOGOUT', 'Sesión cerrada - Usuario: Miguel Echeverria', '2026-06-23 23:05:16.146325', '{"duracion":0,"motivo":"BLOQUEO_PANTALLA_WINDOWS"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (21, 18, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 23:05:25.872066', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (22, 18, 9, 'LOGOUT', 'Sesión cerrada - Usuario: Miguel Echeverria', '2026-06-23 23:05:26.964281', '{"duracion":0,"motivo":"LOGOUT"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (23, 19, 9, 'LOGIN', 'Usuario Miguel Echeverria inició sesión en MAEC', '2026-06-23 23:05:32.811068', '{"cedula":"1728207315"}');
INSERT INTO public.eventos_sistema (id_evento, id_sesion, id_computadora, tipo_evento, descripcion, fecha_evento, detalles_json) VALUES (24, 19, 9, 'LOGOUT', 'Sesión cerrada - Usuario: Miguel Echeverria', '2026-06-23 23:20:40.408529', '{"duracion":15,"motivo":"BLOQUEO_PANTALLA_WINDOWS"}');


--
-- Data for Name: registro_inactividad; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.registro_inactividad (id_inactividad, id_sesion, hora_inicio, hora_fin, minutos) VALUES (1, 4, '2026-06-20 14:30:00', '2026-06-20 14:35:00', 5);
INSERT INTO public.registro_inactividad (id_inactividad, id_sesion, hora_inicio, hora_fin, minutos) VALUES (2, 8, '2026-06-20 14:20:00', '2026-06-20 14:25:00', 5);
INSERT INTO public.registro_inactividad (id_inactividad, id_sesion, hora_inicio, hora_fin, minutos) VALUES (3, 15, '2026-06-23 22:16:44.157333', NULL, 5);


--
-- Data for Name: roles; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.roles (id_rol, nombre, descripcion) VALUES (1, 'ESTUDIANTE', NULL);
INSERT INTO public.roles (id_rol, nombre, descripcion) VALUES (2, 'DOCENTE', NULL);


--
-- Data for Name: sesiones; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (1, 1, 3, '2026-06-20', '2026-06-20 08:10:00', '2026-06-20 10:00:00', 110, 'CLASE', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (2, 2, 4, '2026-06-20', '2026-06-20 09:00:00', '2026-06-20 11:20:00', 140, 'CLASE', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (3, 6, 3, '2026-06-20', '2026-06-20 08:00:00', '2026-06-20 12:00:00', 240, 'CLASE', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (4, 1, 1, '2026-06-20', '2026-06-20 13:15:00', '2026-06-20 15:30:00', 135, 'PRESTAMO', 'INACTIVIDAD', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (5, 3, 2, '2026-06-20', '2026-06-20 14:10:00', '2026-06-20 15:50:00', 100, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (6, 4, 8, '2026-06-20', '2026-06-20 13:00:00', '2026-06-20 16:20:00', 200, 'PRESTAMO', 'APAGADO', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (7, 8, 5, '2026-06-20', '2026-06-20 12:30:00', '2026-06-20 13:40:00', 70, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (8, 9, 1, '2026-06-20', '2026-06-20 13:20:00', '2026-06-20 15:00:00', 100, 'PRESTAMO', 'INACTIVIDAD', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (10, 13, 9, '2026-06-23', '2026-06-23 20:51:11.540711', '2026-06-23 20:59:02.134772', 8, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (11, 13, 9, '2026-06-23', '2026-06-23 21:39:43.750566', '2026-06-23 21:46:18.428087', 7, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (12, 13, 9, '2026-06-23', '2026-06-23 21:47:15.427457', '2026-06-23 22:02:19.043349', 15, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (9, 1, 1, '2026-06-21', '2026-06-21 19:48:49.487006', '2026-06-23 21:46:18.428087', 2997, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (13, 13, 9, '2026-06-23', '2026-06-23 22:08:51.277119', '2026-06-23 22:09:04.552219', 0, 'PRESTAMO', 'BLOQUEO_PANTALLA_WINDOWS', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (14, 13, 9, '2026-06-23', '2026-06-23 22:09:22.396661', '2026-06-23 22:09:27.173276', 0, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (15, 13, 9, '2026-06-23', '2026-06-23 22:09:36.706552', '2026-06-23 23:03:52.362796', 54, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (16, 13, 9, '2026-06-23', '2026-06-23 23:04:34.258045', '2026-06-23 23:04:43.05535', 0, 'PRESTAMO', 'BLOQUEO_PANTALLA_WINDOWS', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (17, 13, 9, '2026-06-23', '2026-06-23 23:04:54.553265', '2026-06-23 23:05:16.140849', 0, 'PRESTAMO', 'BLOQUEO_PANTALLA_WINDOWS', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (18, 13, 9, '2026-06-23', '2026-06-23 23:05:25.86771', '2026-06-23 23:05:26.961152', 0, 'PRESTAMO', 'LOGOUT', 'FINALIZADA');
INSERT INTO public.sesiones (id_sesion, id_usuario, id_computadora, fecha, hora_inicio, hora_fin, duracion_minutos, tipo_uso, cerrado_por, estado) VALUES (19, 13, 9, '2026-06-23', '2026-06-23 23:05:32.78972', '2026-06-23 23:20:40.392045', 15, 'PRESTAMO', 'BLOQUEO_PANTALLA_WINDOWS', 'FINALIZADA');


--
-- Data for Name: ubicaciones; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.ubicaciones (id_ubicacion, nombre, descripcion, activo, fecha_creacion) VALUES (1, 'Biblioteca', 'Área de préstamo académico', true, '2026-06-21 19:38:53.875853');
INSERT INTO public.ubicaciones (id_ubicacion, nombre, descripcion, activo, fecha_creacion) VALUES (2, 'Laboratorio 3A', 'Laboratorio principal', true, '2026-06-21 19:38:53.875853');
INSERT INTO public.ubicaciones (id_ubicacion, nombre, descripcion, activo, fecha_creacion) VALUES (3, 'Laboratorio 3B', 'Laboratorio secundario', true, '2026-06-21 19:38:53.875853');
INSERT INTO public.ubicaciones (id_ubicacion, nombre, descripcion, activo, fecha_creacion) VALUES (4, 'Laboratorio 3D', 'Laboratorio libre', true, '2026-06-21 19:38:53.875853');


--
-- Data for Name: usuarios; Type: TABLE DATA; Schema: public; Owner: postgres
--

INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (2, '1723456782', 'Mateo', 'Mendoza', 'mateo@instituto.edu', 1, 1, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (3, '1723456783', 'Ana', 'Villacis', 'ana@instituto.edu', 1, 2, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (4, '1723456784', 'Luis', 'Guerrero', 'luis@instituto.edu', 1, 3, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (5, '1723456785', 'Andrea', 'Rojas', 'andrea@instituto.edu', 1, 1, false, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (6, '1723456786', 'Carlos', 'Almeida', 'carlos@instituto.edu', 2, 1, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (7, '1723456787', 'Daniela', 'Vera', 'daniela@instituto.edu', 2, 2, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (8, '1723456788', 'Jose', 'Cueva', 'jose@instituto.edu', 1, 3, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (9, '1723456789', 'Valeria', 'Torres', 'valeria@instituto.edu', 1, 1, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (10, '1723456790', 'Kevin', 'Sanchez', 'kevin@instituto.edu', 1, 2, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (11, '1710034065', 'Juan', 'Perez', 'juan@instituto.edu', 1, 1, true, '2026-06-21 19:05:15.753649');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (1, '1712345678', 'Miguel', 'Perez', 'miguel@instituto.edu', 1, 1, true, '2026-06-20 23:05:27.241222');
INSERT INTO public.usuarios (id_usuario, cedula, nombre, apellido, correo, id_rol, id_carrera, activo, fecha_creacion) VALUES (13, '1728207315', 'Miguel', 'Echeverria', 'miguelangel020206@gmail.com', 1, 1, true, '2026-06-23 20:45:03.073606');


--
-- Name: carreras_id_carrera_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.carreras_id_carrera_seq', 3, true);


--
-- Name: computadoras_id_computadora_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.computadoras_id_computadora_seq', 9, true);


--
-- Name: eventos_sistema_id_evento_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.eventos_sistema_id_evento_seq', 24, true);


--
-- Name: registro_inactividad_id_inactividad_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.registro_inactividad_id_inactividad_seq', 3, true);


--
-- Name: roles_id_rol_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.roles_id_rol_seq', 2, true);


--
-- Name: sesiones_id_sesion_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.sesiones_id_sesion_seq', 19, true);


--
-- Name: ubicaciones_id_ubicacion_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.ubicaciones_id_ubicacion_seq', 4, true);


--
-- Name: usuarios_id_usuario_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public.usuarios_id_usuario_seq', 13, true);


--
-- Name: carreras carreras_codigo_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.carreras
    ADD CONSTRAINT carreras_codigo_key UNIQUE (codigo);


--
-- Name: carreras carreras_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.carreras
    ADD CONSTRAINT carreras_pkey PRIMARY KEY (id_carrera);


--
-- Name: computadoras computadoras_ip_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.computadoras
    ADD CONSTRAINT computadoras_ip_key UNIQUE (ip);


--
-- Name: computadoras computadoras_mac_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.computadoras
    ADD CONSTRAINT computadoras_mac_key UNIQUE (mac);


--
-- Name: computadoras computadoras_nombre_equipo_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.computadoras
    ADD CONSTRAINT computadoras_nombre_equipo_key UNIQUE (nombre_equipo);


--
-- Name: computadoras computadoras_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.computadoras
    ADD CONSTRAINT computadoras_pkey PRIMARY KEY (id_computadora);


--
-- Name: eventos_sistema eventos_sistema_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.eventos_sistema
    ADD CONSTRAINT eventos_sistema_pkey PRIMARY KEY (id_evento);


--
-- Name: registro_inactividad registro_inactividad_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.registro_inactividad
    ADD CONSTRAINT registro_inactividad_pkey PRIMARY KEY (id_inactividad);


--
-- Name: roles roles_nombre_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_nombre_key UNIQUE (nombre);


--
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id_rol);


--
-- Name: sesiones sesiones_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sesiones
    ADD CONSTRAINT sesiones_pkey PRIMARY KEY (id_sesion);


--
-- Name: ubicaciones ubicaciones_nombre_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.ubicaciones
    ADD CONSTRAINT ubicaciones_nombre_key UNIQUE (nombre);


--
-- Name: ubicaciones ubicaciones_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.ubicaciones
    ADD CONSTRAINT ubicaciones_pkey PRIMARY KEY (id_ubicacion);


--
-- Name: usuarios usuarios_cedula_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.usuarios
    ADD CONSTRAINT usuarios_cedula_key UNIQUE (cedula);


--
-- Name: usuarios usuarios_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.usuarios
    ADD CONSTRAINT usuarios_pkey PRIMARY KEY (id_usuario);


--
-- Name: idx_cedula; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_cedula ON public.usuarios USING btree (cedula);


--
-- Name: idx_fecha; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_fecha ON public.sesiones USING btree (fecha);


--
-- Name: idx_ip; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_ip ON public.computadoras USING btree (ip);


--
-- Name: idx_pc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_pc ON public.sesiones USING btree (id_computadora);


--
-- Name: idx_usuario; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_usuario ON public.sesiones USING btree (id_usuario);


--
-- Name: sesiones trg_duracion; Type: TRIGGER; Schema: public; Owner: postgres
--

CREATE TRIGGER trg_duracion BEFORE UPDATE ON public.sesiones FOR EACH ROW EXECUTE FUNCTION public.calcular_duracion();


--
-- Name: registro_inactividad trg_inactividad; Type: TRIGGER; Schema: public; Owner: postgres
--

CREATE TRIGGER trg_inactividad BEFORE INSERT OR UPDATE ON public.registro_inactividad FOR EACH ROW EXECUTE FUNCTION public.calcular_inactividad();


--
-- Name: sesiones trg_tipo; Type: TRIGGER; Schema: public; Owner: postgres
--

CREATE TRIGGER trg_tipo BEFORE INSERT ON public.sesiones FOR EACH ROW EXECUTE FUNCTION public.determinar_tipo_uso();


--
-- Name: computadoras computadoras_id_ubicacion_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.computadoras
    ADD CONSTRAINT computadoras_id_ubicacion_fkey FOREIGN KEY (id_ubicacion) REFERENCES public.ubicaciones(id_ubicacion);


--
-- Name: eventos_sistema eventos_sistema_id_computadora_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.eventos_sistema
    ADD CONSTRAINT eventos_sistema_id_computadora_fkey FOREIGN KEY (id_computadora) REFERENCES public.computadoras(id_computadora);


--
-- Name: eventos_sistema eventos_sistema_id_sesion_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.eventos_sistema
    ADD CONSTRAINT eventos_sistema_id_sesion_fkey FOREIGN KEY (id_sesion) REFERENCES public.sesiones(id_sesion);


--
-- Name: registro_inactividad registro_inactividad_id_sesion_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.registro_inactividad
    ADD CONSTRAINT registro_inactividad_id_sesion_fkey FOREIGN KEY (id_sesion) REFERENCES public.sesiones(id_sesion);


--
-- Name: sesiones sesiones_id_computadora_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sesiones
    ADD CONSTRAINT sesiones_id_computadora_fkey FOREIGN KEY (id_computadora) REFERENCES public.computadoras(id_computadora);


--
-- Name: sesiones sesiones_id_usuario_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sesiones
    ADD CONSTRAINT sesiones_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES public.usuarios(id_usuario);


--
-- Name: usuarios usuarios_id_carrera_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.usuarios
    ADD CONSTRAINT usuarios_id_carrera_fkey FOREIGN KEY (id_carrera) REFERENCES public.carreras(id_carrera);


--
-- Name: usuarios usuarios_id_rol_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.usuarios
    ADD CONSTRAINT usuarios_id_rol_fkey FOREIGN KEY (id_rol) REFERENCES public.roles(id_rol);


--
-- PostgreSQL database dump complete
--

\unrestrict mOaQBUJHcqnu9NmQytrcvlL3bMdcLg0Zj9KDCNvFckWQteEqvo5XPFkI0rhlVhh

