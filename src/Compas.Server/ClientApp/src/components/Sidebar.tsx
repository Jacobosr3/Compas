import { useState, type CSSProperties } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { PROFILE_SECTION_PATHS } from "./profileSection";
import { useAuth } from "../hooks/useAuth";
import { ConfirmModal } from "./ConfirmModal";

const items = [
  {
    to: "/",
    label: "Hoy",
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7v5l3 2" />
      </svg>
    ),
  },
  {
    to: "/agenda",
    label: "Agenda",
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <rect x="3" y="5" width="18" height="16" rx="2" />
        <path d="M3 10h18M8 3v4M16 3v4" />
      </svg>
    ),
  },
  {
    to: "/proyectos",
    label: "Proyectos",
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M4 6h16M4 12h10M4 18h7" />
      </svg>
    ),
  },
  {
    to: "/bandeja",
    label: "Bandeja",
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <path d="M20 7l-8-4-8 4 8 4 8-4zM4 7v10l8 4 8-4V7" />
      </svg>
    ),
  },
  {
    to: "/perfil",
    label: "Perfil",
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
        <circle cx="12" cy="8" r="4" />
        <path d="M4 21c0-4 4-6 8-6s8 2 8 6" />
      </svg>
    ),
  },
];

export function Sidebar() {
  const auth = useAuth();
  const { pathname } = useLocation();
  const [confirmingLogout, setConfirmingLogout] = useState(false);

  // Misma regla que NavLink (con `end` solo en "/"), calculada aquí para
  // poder colocar el indicador deslizante bajo la pestaña activa.
  const activeIndex = items.findIndex((item) =>
    item.to === "/perfil"
      ? PROFILE_SECTION_PATHS.includes(pathname)
      : item.to === "/"
        ? pathname === "/"
        : pathname === item.to || pathname.startsWith(item.to + "/"),
  );

  return (
    <aside className="rail-left">
      <span className="rail-aura" aria-hidden="true" />
      <div className="brand">C</div>
      <nav className="nav" style={{ "--count": items.length } as CSSProperties}>
        {activeIndex >= 0 && (
          <span className="nav-indicator" aria-hidden="true" style={{ "--idx": activeIndex } as CSSProperties} />
        )}
        {items.map((item, i) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === "/"}
            className={"nav-item" + (i === activeIndex ? " active" : "")}
          >
            <span className="i">{item.icon}</span>
            <span>{item.label}</span>
          </NavLink>
        ))}
      </nav>
      <button
        className="nav-item rail-account"
        title={auth.email ? `Cerrar sesión (${auth.email})` : "Cerrar sesión"}
        aria-label="Cerrar sesión"
        onClick={() => setConfirmingLogout(true)}
      >
        <span className="i">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
            <path d="M15 4h3a2 2 0 012 2v12a2 2 0 01-2 2h-3M10 8l-4 4 4 4M6 12h10" />
          </svg>
        </span>
        <span>Salir</span>
      </button>
      {confirmingLogout && (
        <ConfirmModal
          title="Cerrar sesión"
          message={`Se cerrará la sesión de ${auth.email ?? "esta cuenta"} en este dispositivo. Para volver a entrar tendrás que iniciar sesión.`}
          confirmLabel="Cerrar sesión"
          onConfirm={auth.logout}
          onCancel={() => setConfirmingLogout(false)}
        />
      )}
    </aside>
  );
}
