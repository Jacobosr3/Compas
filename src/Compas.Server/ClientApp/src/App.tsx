import { BrowserRouter, Routes, Route } from "react-router-dom";
import { Sidebar } from "./components/Sidebar";
import { TodayPage } from "./pages/TodayPage";
import { ProjectsPage } from "./pages/ProjectsPage";
import { ProjectDetailPage } from "./pages/ProjectDetailPage";
import { AgendaPage } from "./pages/AgendaPage";
import { BandejaPage } from "./pages/BandejaPage";

export default function App() {
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
        </Routes>
      </div>
    </BrowserRouter>
  );
}
