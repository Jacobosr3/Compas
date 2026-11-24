import { NavLink } from "react-router-dom";

/** Pestañas de la sección Perfil: el perfil y lo que cuelga de él (entrenos y comidas). */
const tabs = [
  { to: "/perfil", label: "Perfil" },
  { to: "/entrenos", label: "Entrenos" },
  { to: "/comidas", label: "Comidas" },
];

export function ProfileTabs() {
  return (
    <nav className="profile-tabs segmented" aria-label="Secciones del perfil">
      {tabs.map((t) => (
        <NavLink key={t.to} to={t.to} className={({ isActive }) => "profile-tab" + (isActive ? " active" : "")}>
          {t.label}
        </NavLink>
      ))}
    </nav>
  );
}
