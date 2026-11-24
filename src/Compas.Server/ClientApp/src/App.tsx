import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AuthProvider } from "./components/AuthProvider";
import { useAuth } from "./hooks/useAuth";
import { LoginPage } from "./pages/LoginPage";
import { Sidebar } from "./components/Sidebar";
import { TodayPage } from "./pages/TodayPage";
import { ProjectsPage } from "./pages/ProjectsPage";
import { ProjectDetailPage } from "./pages/ProjectDetailPage";
import { AgendaPage } from "./pages/AgendaPage";
import { BandejaPage } from "./pages/BandejaPage";
import { ProfilePage } from "./pages/ProfilePage";
import { MealsPage } from "./pages/MealsPage";

export default function App() {
  return (
    <AuthProvider>
      <AuthGate />
    </AuthProvider>
  );
}

/** Sin sesión solo se ve el inicio de sesión; con sesión, la aplicación. */
function AuthGate() {
  const auth = useAuth();

  if (auth.state === "loading") return <div className="auth-splash">Cargando…</div>;

  if (auth.state === "error") {
    return (
      <main className="auth-page">
        <div className="auth-card">
          <div className="brand">C</div>
          <h1>No se pudo conectar</h1>
          <p className="auth-sub">{auth.error}</p>
          <button className="btn-primary" onClick={auth.reload}>
            Reintentar
          </button>
        </div>
      </main>
    );
  }

  if (auth.state === "anonymous") return <LoginPage info={auth.info} onAuthenticated={auth.setSession} />;

  return (
    <BrowserRouter>
      <div className="shell">
        <Sidebar />
        <Routes>
          <Route path="/" element={<TodayPage />} />
          <Route path="/proyectos" element={<ProjectsPage />} />
          <Route path="/proyectos/:id" element={<ProjectDetailPage />} />
          <Route path="/agenda" element={<AgendaPage />} />
          <Route path="/bandeja" element={<BandejaPage />} />
          {/* La página Entrenos se quitó: la progresión se aplica en las rutinas de Hevy. */}
          <Route path="/entrenos" element={<Navigate to="/perfil" replace />} />
          <Route path="/comidas" element={<MealsPage />} />
          <Route path="/perfil" element={<ProfilePage />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}
