import { computeMetrics } from "@/lib/adminData";
import { goalProgressRatio } from "@/lib/types";

export const dynamic = "force-dynamic";

/** /dashboard — métricas de impacto: jugadores, Huellas, donaciones, ingresos */
export default async function DashboardPage() {
  const m = await computeMetrics();
  const ratio = m.weeklyGoal ? goalProgressRatio(m.weeklyGoal) : 0;

  const cards: Array<{ label: string; value: string }> = [
    { label: "Jugadores totales", value: m.totalPlayers.toLocaleString("es-MX") },
    { label: "Huellas acumuladas", value: m.totalHuellasAllTime.toLocaleString("es-MX") },
    { label: "Donaciones activas", value: String(m.activeDonations) },
    { label: "Donaciones verificadas", value: String(m.verifiedDonations) },
    { label: "USD donados", value: `$${m.totalDonatedUsd.toLocaleString("es-MX")}` },
    { label: "Ingresos netos", value: `$${m.revenueUsd.toLocaleString("es-MX")}` },
    { label: "Refugios verificados", value: String(m.verifiedShelters) },
    { label: "Animales en adopción", value: String(m.adoptableAnimals) },
  ];

  return (
    <div>
      <h1>Dashboard de impacto</h1>
      <div className="cards">
        {cards.map((c) => (
          <div className="card" key={c.label}>
            <div className="value">{c.value}</div>
            <div className="label">{c.label}</div>
          </div>
        ))}
      </div>

      {m.weeklyGoal && (
        <div className="card">
          <h2 style={{ marginTop: 0 }}>Meta semanal — {m.weeklyGoal.shelterName}</h2>
          <p className="muted">{m.weeklyGoal.rewardDescription}</p>
          <div className="progress" role="progressbar" aria-valuenow={ratio * 100}>
            <div style={{ width: `${Math.round(ratio * 100)}%` }} />
          </div>
          <p>
            {m.weeklyGoal.currentHuellas.toLocaleString("es-MX")} /{" "}
            {m.weeklyGoal.targetHuellas.toLocaleString("es-MX")} Huellas (
            {Math.round(ratio * 100)}%)
          </p>
        </div>
      )}
    </div>
  );
}
